using PeopleDesk.Domain.Common;

namespace PeopleDesk.Domain.Attendance;

public class AttendanceRecord : Entity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid EmployeeId { get; set; }

    /// <summary>Work date in the organization's local time zone.</summary>
    public DateOnly WorkDate { get; set; }

    public DateTime? ClockInAtUtc { get; set; }
    public DateTime? ClockOutAtUtc { get; set; }
    public int BreakMinutes { get; set; }

    public int WorkedMinutes { get; set; }
    public int LateMinutes { get; set; }
    public int EarlyDepartureMinutes { get; set; }
    public int OvertimeMinutes { get; set; }

    public AttendanceStatus Status { get; set; } = AttendanceStatus.Present;
    public AttendanceSource Source { get; set; } = AttendanceSource.SelfClock;

    public string? Note { get; set; }
    public string? ManualEntryBy { get; set; }

    /// <summary>Set when the payroll period containing this date is finalized.</summary>
    public bool IsLocked { get; set; }
}

public class AttendanceCorrectionRequest : Entity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid? AttendanceRecordId { get; set; }

    public DateOnly WorkDate { get; set; }
    public DateTime? RequestedClockInAtUtc { get; set; }
    public DateTime? RequestedClockOutAtUtc { get; set; }
    public int? RequestedBreakMinutes { get; set; }

    public string Reason { get; set; } = default!;
    public ReviewStatus Status { get; set; } = ReviewStatus.Pending;

    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewNote { get; set; }
}
