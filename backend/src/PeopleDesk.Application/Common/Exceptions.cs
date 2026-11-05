namespace PeopleDesk.Application.Common;

/// <summary>Base application exception with a stable machine-readable code surfaced to API clients.</summary>
public class AppException : Exception
{
    public string Code { get; }
    public AppException(string code, string message) : base(message) => Code = code;
}

public class NotFoundException : AppException
{
    public NotFoundException(string what) : base("not_found", $"{what} was not found.") { }
    public NotFoundException(string what, Guid id) : base("not_found", $"{what} '{id}' was not found.") { }
}

public class ForbiddenException : AppException
{
    public ForbiddenException(string message = "You do not have permission to perform this action.")
        : base("forbidden", message) { }
}

public class ConflictException : AppException
{
    public ConflictException(string message) : base("conflict", message) { }
}

public class BadRequestException : AppException
{
    public BadRequestException(string message) : base("bad_request", message) { }
}

/// <summary>Business-rule failure reported back to the client (e.g. overlapping leave, payroll state errors).</summary>
public class BusinessRuleException : AppException
{
    public BusinessRuleException(string message) : base("business_rule", message) { }
}
