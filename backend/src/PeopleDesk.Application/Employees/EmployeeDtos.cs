using FluentValidation;
using PeopleDesk.Domain.Common;

namespace PeopleDesk.Application.Employees;

public record EmployeeListDto
{
    public Guid Id { get; init; }
    public string EmployeeNumber { get; init; } = default!;
    public string FullName { get; init; } = default!;
    public string? WorkEmail { get; init; }
    public string? Phone { get; init; }
    public string? Department { get; init; }
    public Guid? DepartmentId { get; init; }
    public string? Position { get; init; }
    public string? Branch { get; init; }
    public string? ManagerName { get; init; }
    public string? EmploymentType { get; init; }
    public string Status { get; init; } = default!;
    public DateOnly HireDate { get; init; }
    public bool HasUserAccount { get; init; }
}

public record EmployeeDetailsDto
{
    public Guid Id { get; init; }
    public string EmployeeNumber { get; init; } = default!;
    public string FirstName { get; init; } = default!;
    public string? MiddleName { get; init; }
    public string LastName { get; init; } = default!;
    public string Gender { get; init; } = default!;
    public DateOnly? DateOfBirth { get; init; }
    public string? NationalId { get; init; }          // only populated for EmployeesViewSensitive
    public string? PhotoUrl { get; init; }
    public string? WorkEmail { get; init; }
    public string? PersonalEmail { get; init; }
    public string? Phone { get; init; }
    public string? AddressLine { get; init; }
    public string? City { get; init; }
    public string? Country { get; init; }
    public Guid? DepartmentId { get; init; }
    public Guid? PositionId { get; init; }
    public Guid? BranchId { get; init; }
    public Guid? ManagerId { get; init; }
    public Guid? EmploymentTypeId { get; init; }
    public Guid? ScheduleId { get; init; }
    public DateOnly HireDate { get; init; }
    public DateOnly? ContractEndDate { get; init; }
    public string Status { get; init; } = default!;
    public DateOnly? TerminationDate { get; init; }
    public string? TerminationReason { get; init; }
    public string PaymentMethod { get; init; } = default!;
    public string? BankName { get; init; }
    public string? BankAccountMasked { get; init; }   // masked unless EmployeesViewSensitive
    public string? TaxIdentificationNumber { get; init; } // only for EmployeesViewSensitive
    public bool HasUserAccount { get; init; }
}

public record CreateEmployeeRequest
{
    public string FirstName { get; init; } = default!;
    public string? MiddleName { get; init; }
    public string LastName { get; init; } = default!;
    public Gender Gender { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public string? NationalId { get; init; }
    public string? WorkEmail { get; init; }
    public string? PersonalEmail { get; init; }
    public string? Phone { get; init; }
    public string? AddressLine { get; init; }
    public string? City { get; init; }
    public Guid? DepartmentId { get; init; }
    public Guid? PositionId { get; init; }
    public Guid? BranchId { get; init; }
    public Guid? ManagerId { get; init; }
    public Guid? EmploymentTypeId { get; init; }
    public Guid? ScheduleId { get; init; }
    public DateOnly HireDate { get; init; }
    public DateOnly? ContractEndDate { get; init; }
    public EmploymentStatus Status { get; init; } = EmploymentStatus.Probation;
    public SalaryPaymentMethod PaymentMethod { get; init; } = SalaryPaymentMethod.Bank;
    public string? BankName { get; init; }
    public string? BankAccount { get; init; }
    public string? TaxIdentificationNumber { get; init; }
    /// <summary>Create a linked self-service user account with this password.</summary>
    public bool CreateUserAccount { get; init; }
    public string? InitialPassword { get; init; }
}

public record UpdateEmployeeRequest
{
    public string FirstName { get; init; } = default!;
    public string? MiddleName { get; init; }
    public string LastName { get; init; } = default!;
    public Gender Gender { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public string? NationalId { get; init; }
    public string? WorkEmail { get; init; }
    public string? PersonalEmail { get; init; }
    public string? Phone { get; init; }
    public string? AddressLine { get; init; }
    public string? City { get; init; }
    public Guid? DepartmentId { get; init; }
    public Guid? PositionId { get; init; }
    public Guid? BranchId { get; init; }
    public Guid? ManagerId { get; init; }
    public Guid? EmploymentTypeId { get; init; }
    public Guid? ScheduleId { get; init; }
    public DateOnly? ContractEndDate { get; init; }
    public SalaryPaymentMethod PaymentMethod { get; init; }
    public string? BankName { get; init; }
    public string? BankAccount { get; init; }
    public string? TaxIdentificationNumber { get; init; }
    public string? ChangeReason { get; init; }
}

public record OffboardRequest
{
    public EmploymentStatus Status { get; init; } = EmploymentStatus.Terminated;
    public DateOnly TerminationDate { get; init; }
    public string? Reason { get; init; }
}

public record EmploymentHistoryDto
{
    public Guid Id { get; init; }
    public Guid? DepartmentId { get; init; }
    public Guid? PositionId { get; init; }
    public Guid? BranchId { get; init; }
    public string? Status { get; init; }
    public DateOnly EffectiveFrom { get; init; }
    public DateOnly? EffectiveTo { get; init; }
    public string? ChangeReason { get; init; }
    public string? Notes { get; init; }
}

public record EmployeeContactDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = default!;
    public string Relationship { get; init; } = default!;
    public string Phone { get; init; } = default!;
    public string? Email { get; init; }
    public bool IsPrimary { get; init; }
}

public record SaveEmployeeContactRequest
{
    public Guid? Id { get; init; }
    public string Name { get; init; } = default!;
    public string Relationship { get; init; } = default!;
    public string Phone { get; init; } = default!;
    public string? Email { get; init; }
    public bool IsPrimary { get; init; }
}

public record EmployeeDocumentDto
{
    public Guid Id { get; init; }
    public string Kind { get; init; } = default!;
    public string Name { get; init; } = default!;
    public string FileName { get; init; } = default!;
    public string ContentType { get; init; } = default!;
    public long SizeBytes { get; init; }
    public string? UploadedBy { get; init; }
    public DateTime UploadedAtUtc { get; init; }
}

public class CreateEmployeeRequestValidator : AbstractValidator<CreateEmployeeRequest>
{
    public CreateEmployeeRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(60);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(60);
        RuleFor(x => x.WorkEmail).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.WorkEmail));
        RuleFor(x => x.PersonalEmail).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.PersonalEmail));
        RuleFor(x => x.HireDate).NotEmpty().Must(d => d <= DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1)))
            .WithMessage("Hire date cannot be more than a year in the future.");
        RuleFor(x => x.DateOfBirth).Must(d => d is null || d < DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Date of birth must be in the past.");
        RuleFor(x => x.CreateUserAccount).Must(x => !x).When(x => string.IsNullOrWhiteSpace(x.InitialPassword))
            .WithMessage("InitialPassword is required when CreateUserAccount is true.");
    }
}

public class UpdateEmployeeRequestValidator : AbstractValidator<UpdateEmployeeRequest>
{
    public UpdateEmployeeRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(60);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(60);
        RuleFor(x => x.WorkEmail).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.WorkEmail));
    }
}

public class OffboardRequestValidator : AbstractValidator<OffboardRequest>
{
    public OffboardRequestValidator()
    {
        RuleFor(x => x.Status).Must(s => s is EmploymentStatus.Terminated or EmploymentStatus.Resigned or EmploymentStatus.Retired)
            .WithMessage("Offboarding status must be Terminated, Resigned or Retired.");
        RuleFor(x => x.TerminationDate).NotEmpty();
    }
}
