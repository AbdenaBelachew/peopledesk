namespace PeopleDesk.Domain.Common;

public enum OrganizationType { Customer = 0, Platform = 1 }

public enum EmploymentStatus
{
    Probation = 0,
    Active = 1,
    OnLeave = 2,
    Suspended = 3,
    Terminated = 4,
    Resigned = 5,
    Retired = 6
}

public enum Gender { Unspecified = 0, Male = 1, Female = 2 }

public enum SalaryPaymentMethod { Bank = 0, Cash = 1, MobileMoney = 2 }

public enum AttendanceStatus
{
    Present = 0,
    Late = 1,
    HalfDay = 2,
    Absent = 3,
    OnLeave = 4,
    Holiday = 5,
    Weekend = 6,
    Remote = 7
}

public enum AttendanceSource { SelfClock = 0, ManualEntry = 1, Correction = 2 }

public enum ReviewStatus { Pending = 0, Approved = 1, Rejected = 2, Cancelled = 3 }

public enum LeaveAccrualFrequency { YearlyUpfront = 0, Monthly = 1 }

public enum LeaveTransactionType
{
    Grant = 0,
    CarryOver = 1,
    Accrual = 2,
    Application = 3,   // days moved into Pending
    Approval = 4,      // Pending -> Used
    Rejection = 5,     // Pending released
    Cancellation = 6,
    HrOverride = 7,
    Adjustment = 8,
    UnpaidLeave = 9    // informational, consumed by payroll
}

public enum PayrollPeriodType { Monthly = 0, BiWeekly = 1, Weekly = 2 }

public enum PayrollRunStatus
{
    Draft = 0,
    Calculated = 1,
    Approved = 2,
    Finalized = 3,
    Reversed = 4
}

public enum PayComponentKind { Earning = 0, Deduction = 1 }

public enum SalaryComponentType
{
    Basic = 0,
    Allowance = 1,
    Bonus = 2,
    Commission = 3,
    Overtime = 4,
    Tax = 5,
    Pension = 6,
    Loan = 7,
    Other = 8
}

public enum SalaryCalculationType { FixedAmount = 0, PercentOfBasic = 1 }

public enum SubscriptionStatus { Trialing = 0, Active = 1, PastDue = 2, Suspended = 3, Cancelled = 4 }

public enum NotificationType
{
    LeaveRequestSubmitted = 0,
    LeaveRequestDecision = 1,
    AttendanceCorrectionDecision = 2,
    PayrollCompleted = 3,
    PayslipPublished = 4,
    EmployeeOnboarded = 5,
    ContractExpiring = 6,
    PendingApprovals = 7,
    ProfileChangeDecision = 8,
    HrRequestResponse = 9,
    Announcement = 10,
    General = 11
}

public enum AuthTokenType { EmailVerification = 0, PasswordReset = 1, TwoFactorPending = 2 }

public enum EmailOutboxStatus { Pending = 0, Sent = 1, Failed = 2 }

public enum AuditAction { Created = 0, Updated = 1, Deleted = 2, Login = 3, PayrollFinalized = 4, PayrollReversed = 5, PermissionChanged = 6, HrOverride = 7 }

public enum HrRequestCategory { General = 0, DocumentRequest = 1, CertificateRequest = 2, Complaint = 3, Other = 4 }

public enum DocumentKind { Contract = 0, IdDocument = 1, Certificate = 2, MedicalRecord = 3, Other = 4 }
