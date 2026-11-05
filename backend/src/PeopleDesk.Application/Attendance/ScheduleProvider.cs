using Microsoft.EntityFrameworkCore;
using PeopleDesk.Application.Common;
using PeopleDesk.Domain.Attendance;
using PeopleDesk.Domain.Common;
using PeopleDesk.Domain.Organizations;

namespace PeopleDesk.Application.Attendance;

/// <summary>
/// Resolves the effective work schedule and holiday calendar for an organization/date.
/// Shared by attendance, leave and payroll calculations.
/// </summary>
public class ScheduleProvider(IAppDbContext db, ICurrentUser user)
{
    /// <summary>Effective schedule day for a local work date (holiday → non-working).</summary>
    public async Task<AttendanceCalculator.ScheduleDay> ResolveForEmployeeAsync(
        Guid organizationId, Guid employeeId, DateOnly date, CancellationToken ct = default)
    {
        var resolved = await db.Employees.AsNoTracking()
            .Where(e => e.Id == employeeId)
            .Select(e => new ValueTuple<Guid?, Guid?>(
                e.ScheduleId,
                db.WorkSchedules
                    .Where(s => s.OrganizationId == e.OrganizationId && s.IsDefault)
                    .Select(s => (Guid?)s.Id).FirstOrDefault()))
            .FirstOrDefaultAsync(ct);
        if (resolved == default) throw new NotFoundException("Employee", employeeId);

        var scheduleIdToUse = resolved.Item1 ?? resolved.Item2;
        var day = await ResolveDayAsync(organizationId, scheduleIdToUse, date, ct);
        return day;
    }

    public async Task<AttendanceCalculator.ScheduleDay> ResolveDayAsync(
        Guid organizationId, Guid? scheduleId, DateOnly date, CancellationToken ct = default)
    {
        // Holiday override.
        var isHoliday = await db.Holidays.AsNoTracking().AnyAsync(h =>
            h.OrganizationId == organizationId && h.Date == date, ct);
        if (isHoliday)
            return new AttendanceCalculator.ScheduleDay(false, 0, 0, 0);

        var day = scheduleId is { } sid
            ? await db.WorkScheduleDays.AsNoTracking()
                .FirstOrDefaultAsync(d => d.ScheduleId == sid && d.DayOfWeek == date.DayOfWeek, ct)
            : null;

        if (day is null)
        {
            var defaultSchedule = await db.WorkSchedules.AsNoTracking()
                .FirstOrDefaultAsync(s => s.OrganizationId == organizationId && s.IsDefault, ct);
            day = defaultSchedule is null
                ? null
                : await db.WorkScheduleDays.AsNoTracking()
                    .FirstOrDefaultAsync(d => d.ScheduleId == defaultSchedule.Id && d.DayOfWeek == date.DayOfWeek, ct);
        }

        return day is null
            ? new AttendanceCalculator.ScheduleDay(date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday, 9 * 60, 17 * 60, 60)
            : new AttendanceCalculator.ScheduleDay(day.IsWorkingDay, day.StartMinutes, day.EndMinutes, day.BreakMinutes);
    }

    public async Task<(TimeZoneInfo TimeZone, OrganizationSettings Settings)> GetOrgContextAsync(
        Guid organizationId, CancellationToken ct = default)
    {
        var org = await db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == organizationId, ct)
            ?? throw new NotFoundException("Organization");
        var settings = await db.OrganizationSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.OrganizationId == organizationId, ct) ?? new OrganizationSettings();

        TimeZoneInfo tz;
        try { tz = TimeZoneInfo.FindSystemTimeZoneById(org.TimeZone); }
        catch { tz = TimeZoneInfo.Utc; }

        return (tz, settings);
    }

    public async Task<ISet<DateOnly>> GetHolidayDatesAsync(Guid organizationId, int year, CancellationToken ct = default)
    {
        var holidays = await db.Holidays.AsNoTracking()
            .Where(h => h.OrganizationId == organizationId && (h.RecurringYearly || h.Date.Year == year))
            .ToListAsync(ct);
        return holidays.Select(h => h.RecurringYearly ? new DateOnly(year, h.Date.Month, h.Date.Day) : h.Date).ToHashSet();
    }
}
