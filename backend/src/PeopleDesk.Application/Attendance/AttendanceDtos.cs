using FluentValidation;
using PeopleDesk.Domain.Common;

namespace PeopleDesk.Application.Attendance;

public record AttendanceDto
{
    public Guid Id { get; init; }
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = default!;
    public string EmployeeNumber { get; init; } = default!;
    public DateOnly WorkDate { get; init; }
    public DateTime? ClockInUtc { get; init; }
    public DateTime? ClockOutUtc { get; init; }
    public int BreakMinutes { get; init; }
    public int WorkedMinutes { get; init; }
    public int LateMinutes { get; init; }
    public int EarlyDepartureMinutes { get; init; }
    public int OvertimeMinutes { get; init; }
    public string Status { get; init; } = default!;
    public string Source { get; init; } = default!;
    public string? Note { get; init; }
    public bool IsLocked { get; init; }
}

public record ClockActionDto
{
    public Guid RecordId { get; init; }
    public string Action { get; init; } = default!;
    public DateTime AtUtc { get; init; }
    public DateOnly WorkDate { get; init; }
    public int LateMinutes { get; init; }
    public int WorkedMinutes { get; init; }
    public string Status { get; init; } = default!;
}

public record ManualAttendanceRequest
{
    public Guid EmployeeId { get; init; }
    public DateOnly WorkDate { get; init; }
    public DateTime? ClockInUtc { get; init; }
    public DateTime? ClockOutUtc { get; init; }
    public int BreakMinutes { get; init; }
    public AttendanceStatus Status { get; init; } = AttendanceStatus.Present;
    public string? Note { get; init; }
}

public record CorrectionRequestDto
{
    public Guid Id { get; init; }
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = default!;
    public DateOnly WorkDate { get; init; }
    public DateTime? RequestedClockInUtc { get; init; }
    public DateTime? RequestedClockOutUtc { get; init; }
    public int? RequestedBreakMinutes { get; init; }
    public string Reason { get; init; } = default!;
    public string Status { get; init; } = default!;
    public string? ReviewedBy { get; init; }
    public DateTime? ReviewedAtUtc { get; init; }
    public string? ReviewNote { get; init; }
}

public record CreateCorrectionRequest
{
    public DateOnly WorkDate { get; init; }
    public DateTime? RequestedClockInUtc { get; init; }
    public DateTime? RequestedClockOutUtc { get; init; }
    public int? RequestedBreakMinutes { get; init; }
    public string Reason { get; init; } = default!;
}

public record ReviewCorrectionRequest
{
    public bool Approve { get; init; }
    public string? ReviewNote { get; init; }
}

public record MonthlyAttendanceSummaryDto
{
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = default!;
    public int Year { get; init; }
    public int Month { get; init; }
    public int WorkingDays { get; init; }
    public int DaysPresent { get; init; }
    public int DaysLate { get; init; }
    public int DaysAbsent { get; init; }
    public int DaysOnLeave { get; init; }
    public int DaysHoliday { get; init; }
    public int TotalWorkedMinutes { get; init; }
    public int TotalOvertimeMinutes { get; init; }
    public int TotalLateMinutes { get; init; }
    public IReadOnlyList<AttendanceDto> Records { get; init; } = [];
}

public class ManualAttendanceRequestValidator : AbstractValidator<ManualAttendanceRequest>
{
    public ManualAttendanceRequestValidator()
    {
        RuleFor(x => x.EmployeeId).NotEmpty();
        RuleFor(x => x.WorkDate).NotEmpty()
            .Must(d => d <= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Work date cannot be in the future.");
        RuleFor(x => x.BreakMinutes).InclusiveBetween(0, 480);
        RuleFor(x => x)
            .Must(x => x.ClockInUtc is null || x.ClockOutUtc is null || x.ClockOutUtc > x.ClockInUtc)
            .WithMessage("Clock-out must be after clock-in.");
    }
}

public class CreateCorrectionRequestValidator : AbstractValidator<CreateCorrectionRequest>
{
    public CreateCorrectionRequestValidator()
    {
        RuleFor(x => x.WorkDate).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
        RuleFor(x => x)
            .Must(x => x.RequestedClockInUtc is not null || x.RequestedClockOutUtc is not null)
            .WithMessage("At least one requested time is required.");
        RuleFor(x => x)
            .Must(x => x.RequestedClockInUtc is null || x.RequestedClockOutUtc is null || x.RequestedClockOutUtc > x.RequestedClockInUtc)
            .WithMessage("Requested clock-out must be after requested clock-in.");
    }
}
