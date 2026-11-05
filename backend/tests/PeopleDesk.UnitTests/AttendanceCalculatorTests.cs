using PeopleDesk.Domain.Attendance;
using PeopleDesk.Domain.Common;
using Xunit;

namespace PeopleDesk.UnitTests;

public class AttendanceCalculatorTests
{
    private static readonly TimeZoneInfo Tz = TimeZoneInfo.FindSystemTimeZoneById("Africa/Addis_Ababa");

    // Standard workday: 09:00–17:00 local, 60-minute break → 420 required minutes.
    private static readonly AttendanceCalculator.ScheduleDay WorkDay = new(true, 9 * 60, 17 * 60, 60);
    private static readonly AttendanceCalculator.ScheduleDay RestDay = new(false, 0, 0, 0);

    private static DateTime Utc(DateOnly date, int localHour, int localMinute)
    {
        var local = date.ToDateTime(new TimeOnly(localHour, localMinute));
        return TimeZoneInfo.ConvertTimeToUtc(local, Tz);
    }

    [Fact]
    public void OnTimeArrival_IsPresent_WithNoLateMinutes()
    {
        var date = new DateOnly(2026, 11, 2); // Monday
        var result = AttendanceCalculator.Evaluate(
            WorkDay, Utc(date, 9, 0), Utc(date, 17, 0), 60, graceMinutes: 10,
            overtimeEnabled: true, overtimeMultiplier: 1.5m, timeZone: Tz);

        Assert.Equal(AttendanceStatus.Present, result.Status);
        Assert.Equal(0, result.LateMinutes);
        Assert.Equal(0, result.EarlyDepartureMinutes);
        Assert.Equal(0, result.OvertimeMinutes);
    }

    [Fact]
    public void LateArrival_WithinGrace_IsNotLate()
    {
        var date = new DateOnly(2026, 11, 2);
        var result = AttendanceCalculator.Evaluate(
            WorkDay, Utc(date, 9, 8), Utc(date, 17, 0), 60, graceMinutes: 10,
            overtimeEnabled: true, overtimeMultiplier: 1.5m, timeZone: Tz);

        Assert.Equal(0, result.LateMinutes);
        Assert.Equal(AttendanceStatus.Present, result.Status);
    }

    [Fact]
    public void LateArrival_BeyondGrace_CountsLateMinutes()
    {
        var date = new DateOnly(2026, 11, 2);
        var result = AttendanceCalculator.Evaluate(
            WorkDay, Utc(date, 9, 30), Utc(date, 17, 0), 60, graceMinutes: 10,
            overtimeEnabled: true, overtimeMultiplier: 1.5m, timeZone: Tz);

        Assert.Equal(20, result.LateMinutes);
        Assert.Equal(AttendanceStatus.Late, result.Status);
    }

    [Fact]
    public void EarlyDeparture_FlagsHalfDay()
    {
        var date = new DateOnly(2026, 11, 2);
        var result = AttendanceCalculator.Evaluate(
            WorkDay, Utc(date, 9, 0), Utc(date, 14, 0), 60, graceMinutes: 10,
            overtimeEnabled: true, overtimeMultiplier: 1.5m, timeZone: Tz);

        Assert.Equal(AttendanceStatus.HalfDay, result.Status);
        Assert.Equal(180, result.EarlyDepartureMinutes);
    }

    [Fact]
    public void OvertimeMinutes_AreRaw_AndPayMultiplierStaysConfigurable()
    {
        var date = new DateOnly(2026, 11, 2);
        // Worked 09:00 → 19:00 = 600 min − 60 break = 540 worked vs 420 required → 120 raw OT minutes.
        var result = AttendanceCalculator.Evaluate(
            WorkDay, Utc(date, 9, 0), Utc(date, 19, 0), 60, graceMinutes: 10,
            overtimeEnabled: true, overtimeMultiplier: 1.5m, timeZone: Tz);

        Assert.Equal(120, result.OvertimeMinutes);
    }

    [Fact]
    public void Overtime_Disabled_YieldsZeroOvertime()
    {
        var date = new DateOnly(2026, 11, 2);
        var result = AttendanceCalculator.Evaluate(
            WorkDay, Utc(date, 9, 0), Utc(date, 19, 0), 60, graceMinutes: 10,
            overtimeEnabled: false, overtimeMultiplier: 1.5m, timeZone: Tz);

        Assert.Equal(0, result.OvertimeMinutes);
    }

    [Fact]
    public void WorkOnRestDay_CountsAllAsOvertime_WhenEnabled()
    {
        var date = new DateOnly(2026, 11, 1); // Sunday
        var result = AttendanceCalculator.Evaluate(
            RestDay, Utc(date, 10, 0), Utc(date, 14, 0), 30, graceMinutes: 10,
            overtimeEnabled: true, overtimeMultiplier: 1.5m, timeZone: Tz);

        Assert.Equal(210, result.OvertimeMinutes); // 240 − 30
        Assert.Equal(AttendanceStatus.Present, result.Status);
    }

    [Fact]
    public void NoClockIn_OnRestDay_IsWeekend_OnWorkDay_IsAbsent()
    {
        var rest = AttendanceCalculator.Evaluate(RestDay, null, null, 0, 10, true, 1.5m, Tz);
        Assert.Equal(AttendanceStatus.Weekend, rest.Status);

        var absent = AttendanceCalculator.Evaluate(WorkDay, null, null, 0, 10, true, 1.5m, Tz);
        Assert.Equal(AttendanceStatus.Absent, absent.Status);
    }

    [Fact]
    public void OpenRecord_WithoutClockOut_CountsElapsedMinutes_CappedAtSchedule()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        var clockIn = Utc(date, 9, 0);
        var result = AttendanceCalculator.Evaluate(
            WorkDay, clockIn, null, 60, graceMinutes: 10, overtimeEnabled: true, overtimeMultiplier: 1.5m, timeZone: Tz);

        Assert.InRange(result.WorkedMinutes, 0, 420);
        Assert.True(result.LateMinutes <= 0);
    }

    [Fact]
    public void CountWorkingDays_SkipsWeekendsAndHolidays()
    {
        // Week of Nov 2–8, 2026: Mon–Fri working, Sat/Sun off; holiday on Nov 4 (Wed).
        ISet<DateOnly> holidays = new HashSet<DateOnly> { new(2026, 11, 4) };
        var count = AttendanceCalculator.CountWorkingDays(
            new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 8),
            d => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? null : WorkDay,
            holidays);

        Assert.Equal(4, count); // Mon, Tue, Thu, Fri
    }

    [Fact]
    public void CountNonWorkingDays_IncludesHolidaysAndRestDays()
    {
        ISet<DateOnly> holidays = new HashSet<DateOnly> { new(2026, 11, 4) };
        var count = AttendanceCalculator.CountNonWorkingDays(
            new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 8),
            d => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? null : WorkDay,
            holidays);

        Assert.Equal(3, count); // Sat, Sun, holiday
    }
}
