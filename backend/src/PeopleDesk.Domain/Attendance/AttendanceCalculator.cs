using PeopleDesk.Domain.Common;

namespace PeopleDesk.Domain.Attendance;

/// <summary>
/// Pure attendance math against a work schedule. All inputs/outputs use UTC timestamps plus the
/// organization's IANA time zone for local-day reasoning. Deterministic and unit-testable.
/// </summary>
public static class AttendanceCalculator
{
    public sealed record ScheduleDay(bool IsWorkingDay, int StartMinutes, int EndMinutes, int BreakMinutes)
    {
        public int RequiredMinutes => IsWorkingDay ? Math.Max(0, EndMinutes - StartMinutes - BreakMinutes) : 0;
    }

    public sealed record Result(
        int WorkedMinutes,
        int LateMinutes,
        int EarlyDepartureMinutes,
        int OvertimeMinutes,
        AttendanceStatus Status);

    /// <summary>
    /// Computes worked/late/early/OT for a closed attendance record.
    /// Grace minutes allow small lateness before flagging Late.
    /// </summary>
    public static Result Evaluate(
        ScheduleDay day,
        DateTime? clockInUtc,
        DateTime? clockOutUtc,
        int breakMinutes,
        int graceMinutes,
        bool overtimeEnabled,
        decimal overtimeMultiplier,
        TimeZoneInfo timeZone)
    {
        var worked = 0;
        var late = 0;
        var early = 0;
        var status = AttendanceStatus.Present;

        if (clockInUtc is null)
        {
            // No clock-in at all.
            if (!day.IsWorkingDay) return new Result(0, 0, 0, 0, AttendanceStatus.Weekend);
            return new Result(0, 0, 0, 0, AttendanceStatus.Absent);
        }

        var clockIn = clockInUtc.Value;
        var localIn = TimeZoneInfo.ConvertTimeFromUtc(clockIn, timeZone);
        var scheduledStart = localIn.Date.AddMinutes(day.StartMinutes);

        if (!day.IsWorkingDay)
        {
            // Work performed on a rest day: everything beyond the break is overtime if enabled.
            worked = clockOutUtc is { } restOut
                ? Math.Max(0, (int)(restOut - clockIn).TotalMinutes - breakMinutes)
                : 0;
            var restOt = overtimeEnabled ? worked : 0;
            return new Result(worked, 0, 0, restOt, AttendanceStatus.Present);
        }

        var lateCandidate = (int)(localIn - scheduledStart).TotalMinutes - graceMinutes;
        late = Math.Max(0, lateCandidate);
        if (late > 0) status = AttendanceStatus.Late;

        if (clockOutUtc is null)
        {
            // No clock-out yet: worked = minutes so far, capped at scheduled length.
            var elapsedSoFar = (int)(DateTime.UtcNow - clockIn).TotalMinutes - breakMinutes;
            worked = Math.Max(0, Math.Min(elapsedSoFar, day.RequiredMinutes));
            return new Result(worked, late, 0, 0, status);
        }

        var clockOut = clockOutUtc.Value;
        var localOut = TimeZoneInfo.ConvertTimeFromUtc(clockOut, timeZone);
        var scheduledEnd = localOut.Date.AddMinutes(day.EndMinutes);

        var gross = (int)(clockOut - clockIn).TotalMinutes;
        worked = Math.Max(0, gross - breakMinutes);

        early = Math.Max(0, (int)(scheduledEnd - localOut).TotalMinutes);
        if (early > 0) status = AttendanceStatus.HalfDay;

        var requiredMinutes = day.RequiredMinutes;
        // Raw overtime minutes. The pay multiplier is applied by the payroll engine, not here,
        // so attendance minutes stay factual and pay rules stay configurable.
        var overtime = overtimeEnabled ? Math.Max(0, worked - requiredMinutes) : 0;

        return new Result(worked, late, early, overtime, status);
    }

    /// <summary>Workday count between two dates inclusive, skipping non-working days and holidays.</summary>
    public static int CountWorkingDays(DateOnly from, DateOnly to, Func<DateOnly, ScheduleDay?> scheduleFor, ISet<DateOnly> holidays)
    {
        if (to < from) return 0;
        var count = 0;
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            if (holidays.Contains(d)) continue;
            var day = scheduleFor(d);
            if (day is { IsWorkingDay: true }) count++;
        }
        return count;
    }

    /// <summary>Number of non-working days (weekend/holiday) inside a leave range — leave days exclude them.</summary>
    public static int CountNonWorkingDays(DateOnly from, DateOnly to, Func<DateOnly, ScheduleDay?> scheduleFor, ISet<DateOnly> holidays)
    {
        if (to < from) return 0;
        var count = 0;
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            if (holidays.Contains(d)) { count++; continue; }
            var day = scheduleFor(d);
            if (day is null || !day.IsWorkingDay) count++;
        }
        return count;
    }
}
