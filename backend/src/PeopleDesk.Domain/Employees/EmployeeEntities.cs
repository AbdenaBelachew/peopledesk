using PeopleDesk.Domain.Common;
using PeopleDesk.Domain.Identity;

namespace PeopleDesk.Domain.Employees;

public class Employee : Entity, IOrganizationScoped, ISoftDeletable, IHasConcurrencyToken
{
    public Guid OrganizationId { get; set; }
    public string EmployeeNumber { get; set; } = default!;

    /// <summary>Self-service identity link. The canonical direction is User.EmployeeId (see User entity);
    /// this column is kept for reporting and uniqueness (one employee per user).</summary>
    public Guid? UserId { get; set; }

    // Personal
    public string FirstName { get; set; } = default!;
    public string? MiddleName { get; set; }
    public string LastName { get; set; } = default!;
    public Gender Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? NationalId { get; set; }            // sensitive
    public string? PhotoUrl { get; set; }

    // Contact
    public string? WorkEmail { get; set; }
    public string? PersonalEmail { get; set; }
    public string? Phone { get; set; }
    public string? AddressLine { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }

    // Employment
    public Guid? DepartmentId { get; set; }
    public Guid? PositionId { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? ManagerId { get; set; }
    public Guid? EmploymentTypeId { get; set; }
    public Guid? ScheduleId { get; set; }

    public DateOnly HireDate { get; set; }
    public DateOnly? ContractEndDate { get; set; }
    public EmploymentStatus Status { get; set; } = EmploymentStatus.Probation;
    public DateOnly? TerminationDate { get; set; }
    public string? TerminationReason { get; set; }

    // Payment (sensitive)
    public SalaryPaymentMethod PaymentMethod { get; set; } = SalaryPaymentMethod.Bank;
    public string? BankName { get; set; }
    public string? BankAccount { get; set; }           // sensitive
    public string? TaxIdentificationNumber { get; set; } // sensitive

    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
}

public class EmployeeContact : Entity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid EmployeeId { get; set; }
    public string Name { get; set; } = default!;
    public string Relationship { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public string? Email { get; set; }
    public bool IsPrimary { get; set; }
}

public class EmployeeDocument : Entity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid EmployeeId { get; set; }
    public DocumentKind Kind { get; set; }
    public string Name { get; set; } = default!;
    public string FileName { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public long SizeBytes { get; set; }
    public string StorageKey { get; set; } = default!;
    public string? UploadedBy { get; set; }
}

public class EmploymentHistory : Entity, IOrganizationScoped
{
    public Guid OrganizationId { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid? DepartmentId { get; set; }
    public Guid? PositionId { get; set; }
    public Guid? BranchId { get; set; }
    public EmploymentStatus? Status { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public string? ChangeReason { get; set; }
    public string? Notes { get; set; }
}
