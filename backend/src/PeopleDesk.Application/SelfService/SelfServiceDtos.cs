using FluentValidation;
using PeopleDesk.Domain.Common;

namespace PeopleDesk.Application.SelfService;

public record ProfileChangeRequestDto
{
    public Guid Id { get; init; }
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = default!;
    public string FieldChangesJson { get; init; } = default!;
    public string Status { get; init; } = default!;
    public string? ReviewedBy { get; init; }
    public DateTime? ReviewedAtUtc { get; init; }
    public string? ReviewNote { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public record SubmitProfileChangeRequest
{
    /// <summary>Dictionary of field → new value (permitted fields only: phone, personalEmail, addressLine, city).</summary>
    public Dictionary<string, string> Changes { get; init; } = new();
}

public record HrRequestDto
{
    public Guid Id { get; init; }
    public Guid EmployeeId { get; init; }
    public string EmployeeName { get; init; } = default!;
    public HrRequestCategory Category { get; init; }
    public string Subject { get; init; } = default!;
    public string Body { get; init; } = default!;
    public string Status { get; init; } = default!;
    public string? Response { get; init; }
    public string? HandledBy { get; init; }
    public DateTime? ClosedAtUtc { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public record SubmitHrRequest
{
    public HrRequestCategory Category { get; init; }
    public string Subject { get; init; } = default!;
    public string Body { get; init; } = default!;
}

public record RespondHrRequest
{
    public bool Approve { get; init; } = true;
    public string Response { get; init; } = default!;
}

public class SubmitHrRequestValidator : AbstractValidator<SubmitHrRequest>
{
    public SubmitHrRequestValidator()
    {
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Body).NotEmpty().MaximumLength(5000);
    }
}

public class RespondHrRequestValidator : AbstractValidator<RespondHrRequest>
{
    public RespondHrRequestValidator() => RuleFor(x => x.Response).NotEmpty().MaximumLength(5000);
}

/// <summary>Fields employees may edit directly about their own profile.</summary>
public static class SelfEditableEmployeeFields
{
    public static readonly string[] Permitted = ["phone", "personalEmail", "addressLine", "city"];
}
