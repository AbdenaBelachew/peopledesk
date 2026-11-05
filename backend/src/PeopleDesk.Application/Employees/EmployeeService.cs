using Microsoft.EntityFrameworkCore;
using FluentValidation;
using PeopleDesk.Application.Auth;
using PeopleDesk.Application.Common;
using PeopleDesk.Domain.Common;
using PeopleDesk.Domain.Employees;
using PeopleDesk.Domain.Identity;
using PeopleDesk.Domain.Organizations;

namespace PeopleDesk.Application.Employees;

public class EmployeeService(
    IAppDbContext db,
    ICurrentUser user,
    IDateTime clock,
    IPasswordHasher hasher,
    IFileStorage storage,
    IEmailOutbox emailOutbox,
    Microsoft.Extensions.Options.IOptions<AuthOptions> authOptions)
{
    private Guid OrgId => user.OrganizationId
        ?? throw new ForbiddenException("No organization context for this request.");

    private bool CanSeeSensitive => user.Has(Permissions.EmployeesViewSensitive);

    // ─── Listing & retrieval ─────────────────────────────────────────────────────

    public async Task<PagedResult<EmployeeListDto>> ListAsync(PagedRequest page, Guid? departmentId, Guid? branchId,
        EmploymentStatus? status, CancellationToken ct = default)
    {
        user.Ensure(Permissions.EmployeesView);

        var query = db.Employees.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var term = page.Search.Trim().ToLower();
            query = query.Where(e =>
                e.FirstName.ToLower().Contains(term) || e.LastName.ToLower().Contains(term) ||
                e.EmployeeNumber.ToLower().Contains(term) ||
                (e.WorkEmail != null && e.WorkEmail.ToLower().Contains(term)));
        }
        if (departmentId is { } dep) query = query.Where(e => e.DepartmentId == dep);
        if (branchId is { } br) query = query.Where(e => e.BranchId == br);
        if (status is { } st) query = query.Where(e => e.Status == st);

        query = page.SortBy?.ToLowerInvariant() switch
        {
            "employeenumber" => page.SortDesc ? query.OrderByDescending(e => e.EmployeeNumber) : query.OrderBy(e => e.EmployeeNumber),
            "hiredate" => page.SortDesc ? query.OrderByDescending(e => e.HireDate) : query.OrderBy(e => e.HireDate),
            "status" => page.SortDesc ? query.OrderByDescending(e => e.Status) : query.OrderBy(e => e.Status),
            _ => page.SortDesc ? query.OrderByDescending(e => e.LastName) : query.OrderBy(e => e.LastName)
        };

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip(page.Skip).Take(Math.Clamp(page.PageSize, 1, 100))
            .Select(e => new EmployeeListDto
            {
                Id = e.Id,
                EmployeeNumber = e.EmployeeNumber,
                FullName = e.FirstName + " " + e.LastName,
                WorkEmail = e.WorkEmail,
                Phone = e.Phone,
                DepartmentId = e.DepartmentId,
                Department = e.DepartmentId != null ? db.Departments.Where(d => d.Id == e.DepartmentId).Select(d => d.Name).FirstOrDefault() : null,
                Position = e.PositionId != null ? db.Positions.Where(p => p.Id == e.PositionId).Select(p => p.Title).FirstOrDefault() : null,
                Branch = e.BranchId != null ? db.Branches.Where(b => b.Id == e.BranchId).Select(b => b.Name).FirstOrDefault() : null,
                ManagerName = e.ManagerId != null ? db.Employees.Where(m => m.Id == e.ManagerId).Select(m => m.FirstName + " " + m.LastName).FirstOrDefault() : null,
                EmploymentType = e.EmploymentTypeId != null ? db.EmploymentTypes.Where(t => t.Id == e.EmploymentTypeId).Select(t => t.Name).FirstOrDefault() : null,
                Status = e.Status.ToString(),
                HireDate = e.HireDate,
                HasUserAccount = e.UserId != null
            })
            .ToListAsync(ct);

        return new PagedResult<EmployeeListDto>(items, page.Page, page.PageSize, total);
    }

    public async Task<EmployeeDetailsDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        user.Ensure(Permissions.EmployeesView);
        var e = await db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Employee", id);
        return ToDetails(e);
    }

    /// <summary>Self-service view of one's own profile (no EmployeesView permission needed).</summary>
    public async Task<EmployeeDetailsDto> GetOwnAsync(CancellationToken ct = default)
    {
        if (user.EmployeeId is not { } employeeId)
            throw new ForbiddenException("Your account is not linked to an employee record.");
        var e = await db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == employeeId, ct)
            ?? throw new NotFoundException("Employee", employeeId);
        return ToSelfDetails(e);
    }

    // ─── Create / update / offboard / delete ─────────────────────────────────────

    public async Task<EmployeeDetailsDto> CreateAsync(CreateEmployeeRequest request, CancellationToken ct = default)
    {
        user.Ensure(Permissions.EmployeesCreate);
        await new CreateEmployeeRequestValidator().ValidateAndThrowAsync(request, ct);
        await EnforceSubscriptionLimitAsync(ct);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var number = await NextEmployeeNumberAsync(ct);
        var employee = new Employee
        {
            OrganizationId = OrgId,
            EmployeeNumber = number,
            FirstName = request.FirstName.Trim(),
            MiddleName = request.MiddleName?.Trim(),
            LastName = request.LastName.Trim(),
            Gender = request.Gender,
            DateOfBirth = request.DateOfBirth,
            NationalId = request.NationalId,
            WorkEmail = request.WorkEmail,
            PersonalEmail = request.PersonalEmail,
            Phone = request.Phone,
            AddressLine = request.AddressLine,
            City = request.City,
            DepartmentId = request.DepartmentId,
            PositionId = request.PositionId,
            BranchId = request.BranchId,
            ManagerId = request.ManagerId,
            EmploymentTypeId = request.EmploymentTypeId,
            ScheduleId = request.ScheduleId,
            HireDate = request.HireDate,
            ContractEndDate = request.ContractEndDate,
            Status = request.Status,
            PaymentMethod = request.PaymentMethod,
            BankName = request.BankName,
            BankAccount = request.BankAccount,
            TaxIdentificationNumber = request.TaxIdentificationNumber
        };

        // Track the employee FIRST: the user account (and its outbox email) references it.
        db.Employees.Add(employee);
        db.EmploymentHistories.Add(new EmploymentHistory
        {
            OrganizationId = OrgId,
            EmployeeId = employee.Id,
            DepartmentId = employee.DepartmentId,
            PositionId = employee.PositionId,
            BranchId = employee.BranchId,
            Status = employee.Status,
            EffectiveFrom = employee.HireDate,
            ChangeReason = "Onboarding"
        });

        if (request.CreateUserAccount && !string.IsNullOrWhiteSpace(request.WorkEmail))
        {
            var email = request.WorkEmail.Trim();
            if (await db.Users.AnyAsync(u => u.NormalizedEmail == email.ToUpperInvariant(), ct))
                throw new ConflictException($"A user account with email '{email}' already exists.");

            var newUser = new User
            {
                OrganizationId = OrgId,
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                PasswordHash = hasher.Hash(request.InitialPassword!),
                FirstName = employee.FirstName,
                LastName = employee.LastName,
                EmployeeId = employee.Id
            };
            employee.UserId = newUser.Id; // mirror column used by offboarding/list filters
            db.Users.Add(newUser);
            var employeeRole = await db.Roles.AsNoTracking()
                .FirstOrDefaultAsync(r => r.OrganizationId == OrgId && r.Name == Permissions.Roles.Employee, ct)
                ?? throw new NotFoundException("Employee role — organization provisioning is broken.");
            db.UserRoles.Add(new UserRole { UserId = newUser.Id, RoleId = employeeRole.Id, User = newUser });

            var baseLink = authOptions.Value.FrontendBaseUrl;
            var (subject, html) = EmailTemplates.Welcome(employee.FirstName, "PeopleDesk");
            await emailOutbox.EnqueueAsync(OrgId, email,
                "Your PeopleDesk account is ready",
                $"<p>Hi {System.Net.WebUtility.HtmlEncode(employee.FirstName)},</p><p>An account was created for you at <b>{baseLink}</b>. Please ask your HR manager for your initial password and change it after first sign-in.</p>",
                ct: ct);
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return ToDetails(employee);
    }

    public async Task<EmployeeDetailsDto> UpdateAsync(Guid id, UpdateEmployeeRequest request, CancellationToken ct = default)
    {
        user.Ensure(Permissions.EmployeesUpdate);
        await new UpdateEmployeeRequestValidator().ValidateAndThrowAsync(request, ct);

        var e = await db.Employees.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Employee", id);

        var structureChanged = e.DepartmentId != request.DepartmentId || e.PositionId != request.PositionId
            || e.BranchId != request.BranchId;

        e.FirstName = request.FirstName.Trim();
        e.MiddleName = request.MiddleName?.Trim();
        e.LastName = request.LastName.Trim();
        e.Gender = request.Gender;
        e.DateOfBirth = request.DateOfBirth;
        e.NationalId = request.NationalId;
        e.WorkEmail = request.WorkEmail;
        e.PersonalEmail = request.PersonalEmail;
        e.Phone = request.Phone;
        e.AddressLine = request.AddressLine;
        e.City = request.City;
        e.DepartmentId = request.DepartmentId;
        e.PositionId = request.PositionId;
        e.BranchId = request.BranchId;
        e.ManagerId = request.ManagerId == id ? null : request.ManagerId;
        e.EmploymentTypeId = request.EmploymentTypeId;
        e.ScheduleId = request.ScheduleId;
        e.ContractEndDate = request.ContractEndDate;
        e.PaymentMethod = request.PaymentMethod;
        e.BankName = request.BankName;
        e.BankAccount = request.BankAccount;
        e.TaxIdentificationNumber = request.TaxIdentificationNumber;

        if (structureChanged)
        {
            var open = await db.EmploymentHistories
                .Where(h => h.EmployeeId == id && h.EffectiveTo == null).ToListAsync(ct);
            foreach (var h in open) h.EffectiveTo = clock.TodayUtc;

            db.EmploymentHistories.Add(new EmploymentHistory
            {
                OrganizationId = OrgId,
                EmployeeId = id,
                DepartmentId = e.DepartmentId,
                PositionId = e.PositionId,
                BranchId = e.BranchId,
                Status = e.Status,
                EffectiveFrom = clock.TodayUtc,
                ChangeReason = request.ChangeReason ?? "Structural update"
            });
        }

        await db.SaveChangesAsync(ct);
        return ToDetails(e);
    }

    public async Task OffboardAsync(Guid id, OffboardRequest request, CancellationToken ct = default)
    {
        user.Ensure(Permissions.EmployeesUpdate);
        await new OffboardRequestValidator().ValidateAndThrowAsync(request, ct);

        var e = await db.Employees.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Employee", id);

        e.Status = request.Status;
        e.TerminationDate = request.TerminationDate;
        e.TerminationReason = request.Reason;

        if (e.UserId is { } userId)
        {
            var account = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
            if (account is not null) account.IsActive = false;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        user.Ensure(Permissions.EmployeesDelete);
        var e = await db.Employees.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException("Employee", id);
        e.IsDeleted = true;
        e.DeletedAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    // ─── Contacts ────────────────────────────────────────────────────────────────

    public async Task<List<EmployeeContactDto>> ListContactsAsync(Guid employeeId, CancellationToken ct = default)
    {
        user.Ensure(Permissions.EmployeesView);
        var contacts = await db.EmployeeContacts.AsNoTracking()
            .Where(c => c.EmployeeId == employeeId).OrderByDescending(c => c.IsPrimary).ThenBy(c => c.Name).ToListAsync(ct);
        return contacts.Select(c => new EmployeeContactDto
        {
            Id = c.Id, Name = c.Name, Relationship = c.Relationship, Phone = c.Phone, Email = c.Email, IsPrimary = c.IsPrimary
        }).ToList();
    }

    public async Task<EmployeeContactDto> SaveContactAsync(Guid employeeId, SaveEmployeeContactRequest request, CancellationToken ct = default)
    {
        user.Ensure(Permissions.EmployeesUpdate);
        await EnsureEmployeeExistsAsync(employeeId, ct);

        EmployeeContact contact;
        if (request.Id is { } id)
        {
            contact = await db.EmployeeContacts.FirstOrDefaultAsync(c => c.Id == id && c.EmployeeId == employeeId, ct)
                ?? throw new NotFoundException("Contact", id);
        }
        else
        {
            contact = new EmployeeContact { OrganizationId = OrgId, EmployeeId = employeeId };
            db.EmployeeContacts.Add(contact);
        }

        contact.Name = request.Name.Trim();
        contact.Relationship = request.Relationship.Trim();
        contact.Phone = request.Phone.Trim();
        contact.Email = request.Email;
        contact.IsPrimary = request.IsPrimary;

        if (request.IsPrimary)
        {
            var others = await db.EmployeeContacts.Where(c => c.EmployeeId == employeeId && c.Id != contact.Id).ToListAsync(ct);
            foreach (var o in others) o.IsPrimary = false;
        }

        await db.SaveChangesAsync(ct);
        return new EmployeeContactDto
        {
            Id = contact.Id, Name = contact.Name, Relationship = contact.Relationship,
            Phone = contact.Phone, Email = contact.Email, IsPrimary = contact.IsPrimary
        };
    }

    public async Task DeleteContactAsync(Guid employeeId, Guid contactId, CancellationToken ct = default)
    {
        user.Ensure(Permissions.EmployeesUpdate);
        var contact = await db.EmployeeContacts.FirstOrDefaultAsync(c => c.Id == contactId && c.EmployeeId == employeeId, ct)
            ?? throw new NotFoundException("Contact", contactId);
        db.EmployeeContacts.Remove(contact);
        await db.SaveChangesAsync(ct);
    }

    // ─── Documents ───────────────────────────────────────────────────────────────

    private static readonly string[] AllowedDocExtensions = [".pdf", ".png", ".jpg", ".jpeg", ".webp", ".doc", ".docx"];
    private const long MaxDocBytes = 10 * 1024 * 1024;

    public async Task<List<EmployeeDocumentDto>> ListDocumentsAsync(Guid employeeId, CancellationToken ct = default)
    {
        user.Ensure(Permissions.EmployeesView);
        var docs = await db.EmployeeDocuments.AsNoTracking()
            .Where(d => d.EmployeeId == employeeId).OrderByDescending(d => d.CreatedAtUtc).ToListAsync(ct);
        return docs.Select(d => new EmployeeDocumentDto
        {
            Id = d.Id, Kind = d.Kind.ToString(), Name = d.Name, FileName = d.FileName,
            ContentType = d.ContentType, SizeBytes = d.SizeBytes, UploadedBy = d.UploadedBy,
            UploadedAtUtc = d.CreatedAtUtc
        }).ToList();
    }

    public async Task<EmployeeDocumentDto> UploadDocumentAsync(Guid employeeId, DocumentKind kind, string name,
        string fileName, string contentType, long sizeBytes, Stream content, CancellationToken ct = default)
    {
        user.Ensure(Permissions.EmployeesUpdate);
        await EnsureEmployeeExistsAsync(employeeId, ct);

        if (sizeBytes == 0)
            throw new BadRequestException("No file was uploaded.");
        if (sizeBytes > MaxDocBytes)
            throw new BadRequestException("File exceeds the 10 MB limit.");

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedDocExtensions.Contains(ext))
            throw new BadRequestException($"File type '{ext}' is not allowed.");

        var key = await storage.SaveAsync(OrgId, "employee-docs", content, ext, ct);

        var doc = new EmployeeDocument
        {
            OrganizationId = OrgId,
            EmployeeId = employeeId,
            Kind = kind,
            Name = string.IsNullOrWhiteSpace(name) ? fileName : name.Trim(),
            FileName = fileName,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            SizeBytes = sizeBytes,
            StorageKey = key,
            UploadedBy = user.Email
        };
        db.EmployeeDocuments.Add(doc);
        await db.SaveChangesAsync(ct);

        return new EmployeeDocumentDto
        {
            Id = doc.Id, Kind = doc.Kind.ToString(), Name = doc.Name, FileName = doc.FileName,
            ContentType = doc.ContentType, SizeBytes = doc.SizeBytes, UploadedBy = doc.UploadedBy,
            UploadedAtUtc = doc.CreatedAtUtc
        };
    }

    public async Task<(Stream Content, string ContentType, string FileName)?> DownloadDocumentAsync(Guid employeeId, Guid documentId, CancellationToken ct = default)
    {
        user.Ensure(Permissions.EmployeesView);
        var doc = await db.EmployeeDocuments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId && d.EmployeeId == employeeId, ct)
            ?? throw new NotFoundException("Document", documentId);
        var opened = await storage.OpenAsync(doc.StorageKey, ct);
        return opened is null ? null : (opened.Value.Content, opened.Value.ContentType, doc.FileName);
    }

    public async Task DeleteDocumentAsync(Guid employeeId, Guid documentId, CancellationToken ct = default)
    {
        user.Ensure(Permissions.EmployeesUpdate);
        var doc = await db.EmployeeDocuments.FirstOrDefaultAsync(d => d.Id == documentId && d.EmployeeId == employeeId, ct)
            ?? throw new NotFoundException("Document", documentId);
        await storage.DeleteAsync(doc.StorageKey, ct);
        db.EmployeeDocuments.Remove(doc);
        await db.SaveChangesAsync(ct);
    }

    // ─── History ─────────────────────────────────────────────────────────────────

    public async Task<List<EmploymentHistoryDto>> ListHistoryAsync(Guid employeeId, CancellationToken ct = default)
    {
        user.Ensure(Permissions.EmployeesView);
        var rows = await db.EmploymentHistories.AsNoTracking()
            .Where(h => h.EmployeeId == employeeId).OrderByDescending(h => h.EffectiveFrom).ToListAsync(ct);
        return rows.Select(h => new EmploymentHistoryDto
        {
            Id = h.Id, DepartmentId = h.DepartmentId, PositionId = h.PositionId, BranchId = h.BranchId,
            Status = h.Status?.ToString(), EffectiveFrom = h.EffectiveFrom, EffectiveTo = h.EffectiveTo,
            ChangeReason = h.ChangeReason, Notes = h.Notes
        }).ToList();
    }

    // ─── helpers ─────────────────────────────────────────────────────────────────

    private async Task EnforceSubscriptionLimitAsync(CancellationToken ct)
    {
        var sub = await db.Subscriptions.Include(s => s.Plan).AsNoTracking()
            .FirstOrDefaultAsync(s => s.OrganizationId == OrgId, ct);
        if (sub is null) return;

        if (sub.Status is SubscriptionStatus.Suspended or SubscriptionStatus.Cancelled)
            throw new BusinessRuleException("Your subscription is not active. Contact the platform administrator.");

        var current = await db.Employees.AsNoTracking().CountAsync(ct);
        if (current >= sub.Plan.MaxEmployees)
            throw new BusinessRuleException(
                $"Employee limit reached for the {sub.Plan.Name} plan ({sub.Plan.MaxEmployees} employees). Upgrade the subscription to add more.");
    }

    private async Task<string> NextEmployeeNumberAsync(CancellationToken ct)
    {
        // Numbering is per-year (EMP-2026-0001…), derived from existing numbers so it stays
        // correct after imports or manual numbering.
        var yearPrefix = $"EMP-{clock.TodayUtc:yyyy}-";
        var existing = await db.Employees.IgnoreQueryFilters()
            .Where(e => e.OrganizationId == OrgId && e.EmployeeNumber.StartsWith(yearPrefix))
            .Select(e => e.EmployeeNumber).ToListAsync(ct);
        var maxSeq = existing
            .Select(n => int.TryParse(n[yearPrefix.Length..], out var v) ? v : 0)
            .DefaultIfEmpty(0).Max();

        return yearPrefix + (maxSeq + 1).ToString("D4");
    }

    private async Task EnsureEmployeeExistsAsync(Guid employeeId, CancellationToken ct)
    {
        _ = await db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId, ct)
            ?? throw new NotFoundException("Employee", employeeId);
    }

    private EmployeeDetailsDto ToDetails(Employee e) => new()
    {
        Id = e.Id,
        EmployeeNumber = e.EmployeeNumber,
        FirstName = e.FirstName,
        MiddleName = e.MiddleName,
        LastName = e.LastName,
        Gender = e.Gender.ToString(),
        DateOfBirth = e.DateOfBirth,
        NationalId = CanSeeSensitive ? e.NationalId : null,
        PhotoUrl = e.PhotoUrl,
        WorkEmail = e.WorkEmail,
        PersonalEmail = e.PersonalEmail,
        Phone = e.Phone,
        AddressLine = e.AddressLine,
        City = e.City,
        DepartmentId = e.DepartmentId,
        PositionId = e.PositionId,
        BranchId = e.BranchId,
        ManagerId = e.ManagerId,
        EmploymentTypeId = e.EmploymentTypeId,
        ScheduleId = e.ScheduleId,
        HireDate = e.HireDate,
        ContractEndDate = e.ContractEndDate,
        Status = e.Status.ToString(),
        TerminationDate = e.TerminationDate,
        TerminationReason = e.TerminationReason,
        PaymentMethod = e.PaymentMethod.ToString(),
        BankName = CanSeeSensitive ? e.BankName : null,
        BankAccountMasked = e.BankAccount is { Length: > 4 } bank
            ? (CanSeeSensitive ? bank : "•••• " + bank[^4..])
            : null,
        TaxIdentificationNumber = CanSeeSensitive ? e.TaxIdentificationNumber : null,
        HasUserAccount = e.UserId is not null
    };

    private EmployeeDetailsDto ToSelfDetails(Employee e)
    {
        var dto = ToDetails(e);
        return dto; // self view: masked unless user has the sensitive permission anyway
    }
}
