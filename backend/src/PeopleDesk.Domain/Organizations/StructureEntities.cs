using PeopleDesk.Domain.Common;

namespace PeopleDesk.Domain.Organizations;

public class Department : Entity, IOrganizationScoped, ISoftDeletable
{
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = default!;
    public string? Code { get; set; }
    public string? Description { get; set; }
    public Guid? ManagerEmployeeId { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
}

public class Branch : Entity, IOrganizationScoped, ISoftDeletable
{
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = default!;
    public string? Code { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? Phone { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
}

public class Position : Entity, IOrganizationScoped, ISoftDeletable
{
    public Guid OrganizationId { get; set; }
    public string Title { get; set; } = default!;
    public string? Code { get; set; }
    public string? Level { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
}

public class EmploymentType : Entity, IOrganizationScoped, ISoftDeletable
{
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = default!;
    public string? Description { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
}

public class WorkSchedule : Entity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = default!;
    public bool IsDefault { get; set; }

    public ICollection<WorkScheduleDay> Days { get; set; } = new List<WorkScheduleDay>();
}

public class WorkScheduleDay : Entity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid ScheduleId { get; set; }
    public WorkSchedule Schedule { get; set; } = default!;
    public DayOfWeek DayOfWeek { get; set; }
    public bool IsWorkingDay { get; set; } = true;
    /// <summary>Local start time-of-day, minutes since midnight.</summary>
    public int StartMinutes { get; set; } = 9 * 60;
    /// <summary>Local end time-of-day, minutes since midnight.</summary>
    public int EndMinutes { get; set; } = 17 * 60;
    public int BreakMinutes { get; set; } = 60;
}

public class Holiday : Entity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = default!;
    public DateOnly Date { get; set; }
    public bool RecurringYearly { get; set; }
    public string? Description { get; set; }
}

public class Announcement : Entity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public string Title { get; set; } = default!;
    public string Body { get; set; } = default!;
    public DateTime PublishedAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
}
