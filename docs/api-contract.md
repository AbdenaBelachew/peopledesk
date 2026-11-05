# PeopleDesk API Contract (v1) — Frontend Reference

Base URL: `/api/v1` (Vite dev proxy → `http://localhost:5090`). JSON everywhere; enums as strings.

## Auth & tokens
- Access token: JWT, **in memory only** (never localStorage). Header: `Authorization: Bearer <token>`.
- Refresh token: httpOnly cookie `pd_rt` scoped to `/api/v1/auth`, set automatically by login/register/refresh endpoints.
- On 401 with expired token: POST `/auth/refresh` (sends cookie) → new access token; retry original request. If refresh fails → logout.
- All error responses: `{ "error": { "code", "message", "errors?" } }` where `errors` is a field→messages map for validation failures (HTTP 400).

### Endpoints — AuthController
| Method & path | Body | Response |
|---|---|---|
| POST `/auth/register` | `{organizationName, slug, firstName, lastName, email, password, country}` | `{accessToken, accessTokenExpiresAtUtc, userId, organizationId, organizationName}` |
| POST `/auth/login` | `{email, password, code?}` | same as register — or `{twoFactorRequired: true, pendingToken}` |
| POST `/auth/verify-2fa` | `{pendingToken, code}` | same as register |
| POST `/auth/refresh` | (cookie) | same as register |
| POST `/auth/logout?all=` | — | 204 |
| POST `/auth/forgot-password` | `{email}` | 204 |
| POST `/auth/reset-password` | `{token, newPassword}` | 204 |
| POST `/auth/verify-email` | `{token}` | 204 |
| POST `/auth/resend-verification` | `{email}` | 204 |
| POST `/auth/2fa/setup` | — | `{secret, otpAuthUri}` |
| POST `/auth/2fa/enable` | `{code}` | 204 |
| POST `/auth/2fa/disable` | `{password}` | 204 |
| GET `/auth/me` | — | `MeResponse` (below) |

`MeResponse`: `{userId, organizationId, email, firstName, lastName, employeeId?, isPlatformAdmin, emailVerified, twoFactorEnabled, permissions: string[], roles: string[], organizationName, organizationSlug, logoUrl?, brandColor?, timeZone, currency, subscription?: {planName, planCode, maxEmployees, status, trialEndsAtUtc?, currentPeriodEndUtc?}}`

### Organizations
- GET `/organizations/current` → `{id, name, slug, email?, phone?, logoUrl?, brandColor?, addressLine?, city?, country?, timeZone, currency}`
- PUT `/organizations/current` (perm org.update) — same shape as GET body (name, email, phone, logoUrl, brandColor, addressLine, city, country, timeZone, currency)
- GET/PUT `/organizations/settings` → `{fiscalYearStartMonth, lateGraceMinutes, overtimeEnabled, overtimeMultiplier, requireApprovalForSensitiveProfileChanges, autoApproveLeaveWithoutManager}`
- GET `/organizations/dashboard` → `{totalEmployees, activeEmployees, onProbation, departments, branches, pendingLeaveRequests, pendingCorrections, pendingProfileChanges, nextPayrollDate?}`
- Structure CRUD: `/organizations/departments|branches|positions|employment-types` — GET list → `[{id, name, code?, description?, managerEmployeeId?, employeeCount}]`; POST `{name, code?, description?, managerEmployeeId?, level?}`; PUT `/{id}`; DELETE `/{id}` (perms structure.view / structure.manage)
- Work schedules: GET `/organizations/work-schedules` → `[{id, name, isDefault, days: [{dayOfWeek, isWorkingDay, startTime "HH:mm", endTime, breakMinutes}]}]`; POST same shape `{id?, name, isDefault, days}`
- Holidays: GET `/organizations/holidays?year=` → `[{id, name, date, recurringYearly, description?}]`; POST `{id?, name, date, recurringYearly, description?}`; DELETE `/{id}`
- Announcements: GET `/organizations/announcements?includeExpired=` → `[{id, title, body, publishedAtUtc, expiresAtUtc?}]`; POST `{id?, title, body, expiresAtUtc?}`

### Employees
- GET `/employees?page=&pageSize=&search=&sortBy=&sortDesc=&departmentId=&branchId=&status=` → `{items: [{id, employeeNumber, fullName, workEmail?, phone?, department?, departmentId?, position?, branch?, managerName?, employmentType?, status, hireDate, hasUserAccount}], page, pageSize, totalCount, totalPages}`
- GET `/employees/me` → self profile (no permission needed; linked employee accounts)
- GET/PUT `/employees/{id}` (employees.view / employees.update) — details: `{id, employeeNumber, firstName, middleName?, lastName, gender, dateOfBirth?, nationalId? (only with employees.viewSensitive), photoUrl?, workEmail?, personalEmail?, phone?, addressLine?, city?, departmentId?, positionId?, branchId?, managerId?, employmentTypeId?, scheduleId?, hireDate, contractEndDate?, status, terminationDate?, terminationReason?, paymentMethod, bankName?, bankAccountMasked?, taxIdentificationNumber?, hasUserAccount}`
- POST `/employees` (employees.create) `{firstName, lastName, middleName?, gender, dateOfBirth?, nationalId?, workEmail?, personalEmail?, phone?, addressLine?, city?, departmentId?, positionId?, branchId?, managerId?, employmentTypeId?, scheduleId?, hireDate, contractEndDate?, status?, paymentMethod?, bankName?, bankAccount?, taxIdentificationNumber?, createUserAccount?, initialPassword?}`
- POST `/employees/{id}/offboard` `{status: "Terminated"|"Resigned"|"Retired", terminationDate, reason?}`
- DELETE `/employees/{id}` (employees.delete) — soft delete
- Contacts: GET/POST `/employees/{id}/contacts` `{id?, name, relationship, phone, email?, isPrimary}`; DELETE `/employees/{id}/contacts/{contactId}`
- Documents: GET `/employees/{id}/documents` → `[{id, kind, name, fileName, contentType, sizeBytes, uploadedBy?, uploadedAtUtc}]`; POST multipart `file` + form fields `kind`, `name` (max 10MB; pdf/png/jpg/webp/doc/docx); GET `/employees/{id}/documents/{docId}/download` (binary); DELETE `/{docId}`
- History: GET `/employees/{id}/history` → `[{id, departmentId?, positionId?, branchId?, status?, effectiveFrom, effectiveTo?, changeReason?, notes?}]`

### Salary structures (employees.viewSensitive)
- GET `/employees/{id}/salary-structures` → `[{id, employeeId, effectiveFrom, effectiveTo?, currency, notes?, components: [{id, kind: "Earning"|"Deduction", code, label, type, calculationType: "FixedAmount"|"PercentOfBasic", amount, isTaxable, isActive}]}]`
- POST `/employees/{id}/salary-structures` `{effectiveFrom, currency, notes?, components: [{kind, code, label, type, calculationType?, amount, isTaxable?, isActive?}]}` (exactly one BASIC earning required)
- DELETE `/employees/{id}/salary-structures/{structureId}` (blocked if used by finalized payslips)

### Attendance
- POST `/attendance/clock-in` / POST `/attendance/clock-out` (self; linked employee) → `{recordId, action, atUtc, workDate, lateMinutes, workedMinutes, status}`
- GET `/attendance/today` → record or null
- GET `/attendance?page=&pageSize=&employeeId=&from=&to=` (viewAll; else own) → paged `[{id, employeeId, employeeName, employeeNumber, workDate, clockInUtc?, clockOutUtc?, breakMinutes, workedMinutes, lateMinutes, earlyDepartureMinutes, overtimeMinutes, status, source, note?, isLocked}]`
- POST `/attendance/manual` (attendance.manage) `{employeeId, workDate, clockInUtc?, clockOutUtc?, breakMinutes, status, note?}`
- Corrections: POST `/attendance/corrections` `{workDate, requestedClockInUtc?, requestedClockOutUtc?, requestedBreakMinutes?, reason}`; GET `/attendance/corrections?status=`; POST `/attendance/corrections/{id}/review` (attendance.approve) `{approve, reviewNote?}`
- GET `/attendance/summary/{employeeId}/{year}/{month}` → `{employeeId, employeeName, year, month, workingDays, daysPresent, daysLate, daysAbsent, daysOnLeave, daysHoliday, totalWorkedMinutes, totalOvertimeMinutes, totalLateMinutes, records: [...]}`

### Leave
- GET `/leave/types`; POST `/leave/types` (leave.manage) `{id?, name, code, colorHex?, isPaid, requiresApproval, description?, annualEntitlementDays, accrualFrequency: "YearlyUpfront"|"Monthly", carryOverMaxDays, minNoticeDays, maxConsecutiveDays, allowHalfDays}`
- GET `/leave/balances/{employeeId}?year=` → `[{leaveTypeId, typeName, typeCode, colorHex?, year, entitledDays, carriedOverDays, accruedDays, usedDays, pendingDays, adjustedDays, available}]`
- POST `/leave/balances/{employeeId}/{leaveTypeId}/adjust` (leave.manage) `{days, note?}`
- GET `/leave/transactions/{employeeId}?leaveTypeId=&year=` → `[{id, ..., type, days, balanceAfter, leaveRequestId?, description?, actor?, createdAtUtc}]`
- GET `/leave/requests?page=&employeeId=&status=&leaveTypeId=` → paged `[{id, employeeId, employeeName, employeeNumber, leaveTypeId, typeName, colorHex?, startDate, endDate, days, isPaid, reason?, status: "Pending"|"Approved"|"Rejected"|"Cancelled", reviewedBy?, reviewedAtUtc?, reviewNote?, hrOverride, cancelledAtUtc?}]`
- POST `/leave/requests` `{employeeId?, leaveTypeId, startDate, endDate, isHalfDay?, reason?}`
- POST `/leave/requests/{id}/approve|reject` (leave.approve) `{approve, reviewNote?, isHrOverride?}` — HR override needs leave.manage
- POST `/leave/requests/{id}/cancel` (requester or HR)
- GET `/leave/calendar?from=&to=&departmentId=` (leave.viewAll) → `[{requestId, employeeId, employeeName, leaveTypeId, typeName, colorHex?, startDate, endDate, status}]`

### Payroll
- GET `/payroll/rule-sets` → `[{id, jurisdictionCode, name, version, effectiveFrom, effectiveTo?, isActive, pensionEmployeeRate, pensionEmployerRate, bracketCount}]`
- GET `/payroll/rule-sets/{id}` → adds `brackets: [{lowerBound, upperBound?, rate, quickDeduction}]`
- GET `/payroll/periods` → `[{id, name, type, year, month?, startDate, endDate, payDate?, closed, activeRunId?, runStatus?, totalNet?}]`
- POST `/payroll/periods` (payroll.run) `{type: "Monthly", year, month, payDate?}`; DELETE `/payroll/periods/{id}` (only when no run)
- POST `/payroll/runs` `{payrollPeriodId}` → `{id, payrollPeriodId, periodName, status: "Draft"|"Calculated"|"Approved"|"Finalized"|"Reversed", totalGross, totalDeductions, totalNet, employeeCount, calculatedAtUtc?, calculatedBy?, approvedBy?, finalizedBy?, finalizedAtUtc?, reversalNote?, skippedEmployees: []}`
- GET `/payroll/runs/{id}`; GET `/payroll/runs/{id}/items?page=` → paged `[{id, employeeId, employeeName, employeeNumber, currency, daysWorked, unpaidLeaveDays, overtimeHours, basicSalary, totalEarnings, totalDeductions, grossSalary, netSalary, earnings: [{code, label, type, amount}], deductions: [...]}]`
- POST `/payroll/runs/{id}/calculate` (payroll.run) — recalculates; returns run + skippedEmployees
- POST `/payroll/runs/{id}/approve` (payroll.approve)
- POST `/payroll/runs/{id}/finalize` (payroll.finalize) — generates payslips, locks period + attendance
- POST `/payroll/runs/{id}/reverse` (payroll.reverse) `{note}` (required)
- POST `/payroll/runs/{id}/adjustments` (payroll.run) `{employeeId, kind: "Earning"|"Deduction", code, label, amount, note?}` — only Draft/Calculated

### Payslips
- GET `/payslips?page=&employeeId=&periodId=` (viewAll else own) → paged `[{id, employeeId, employeeName, employeeNumber, payslipNumber, periodName, currency, grossPay, totalDeductions, netPay, issuedAtUtc, publishedAtUtc?}]`
- GET `/payslips/{id}/download` → `application/pdf` (own only unless payslips.viewAll)
- POST `/payslips/runs/{runId}/publish` (payslips.publish) → `{published}`

### Self-service & notifications
- POST `/self-service/profile-changes` `{changes: {phone?, personalEmail?, addressLine?, city?}}`
- GET `/self-service/profile-changes?status=` (hrRequests.handle); POST `/self-service/profile-changes/{id}/review` `{approve, reviewNote?}`
- POST `/self-service/hr-requests` `{category: "General"|"DocumentRequest"|"CertificateRequest"|"Complaint"|"Other", subject, body}`; GET `/self-service/hr-requests?status=`; POST `/self-service/hr-requests/{id}/respond` `{approve, response}`
- GET `/notifications?page=&unreadOnly=` → paged `[{id, type, title, body, link?, readAtUtc?, createdAtUtc}]`; GET `/notifications/unread-count` → number; POST `/notifications/{id}/read`; POST `/notifications/read-all`

### Reports (reports.view unless noted)
- GET `/reports/headcount` → `{total, active, onProbation, terminatedOrResigned, byDepartment: [{label, count}], byBranch, byEmploymentType, byGender}`
- GET `/reports/attendance-trends?from=&to=` → `{from, to, points: [{date, present, late, absent, onLeave, overtimeMinutes}], avgLateMinutesPerDay, totalOvertimeMinutes}`
- GET `/reports/leave-utilization?year=` → `{year, byType: [{typeName, typeCode, colorHex?, entitled, used, pending}]}`
- GET `/reports/payroll-expenses` → `{periods: [{period, gross, deductions, net, employees}], totalGross, totalNet, totalOvertimeCost}`
- GET `/reports/turnover?year=` → `{year, startingHeadcount, newHires, exits, turnoverRate, exitsByMonth: [{label, count}]}`
- Exports: GET `/reports/headcount/export?format=true` (true = xlsx, false/omitted = csv); GET `/reports/attendance/export?from=&to=&format=`; GET `/reports/payroll/{runId}/export?format=`

### Platform (platform.manage; platform admins target a tenant via `X-Tenant-Id` header)
- GET `/platform/stats` → `{tenants, activeTenants, suspendedTenants, users, employees, finalizedPayrollRuns, trialSubscriptions}`
- GET `/platform/tenants?search=` → paged `[{id, name, slug, email?, country?, isActive, createdAtUtc, planName?, subscriptionStatus?, employeeCount, userCount}]`
- POST `/platform/tenants/{id}/suspend` | `/activate`; POST `/platform/tenants/{id}/subscription` `{planId, status: "Trialing"|"Active"|"PastDue"|"Suspended"|"Cancelled"}`
- GET `/platform/plans` → `[{id, code, name, maxEmployees, pricePerMonth, description?, isActive}]`; POST `/platform/plans` `{id?, code, name, maxEmployees, pricePerMonth, description?, isActive?}`

## Permission codes
`org.view org.update settings.manage structure.view structure.manage employees.view employees.viewSensitive employees.create employees.update employees.delete attendance.viewOwn attendance.viewAll attendance.manage attendance.approve leave.viewOwn leave.viewAll leave.approve leave.manage payroll.view payroll.run payroll.approve payroll.finalize payroll.reverse payslips.viewOwn payslips.viewAll payslips.publish users.manage roles.manage reports.view selfService hrRequests.handle announcements.manage platform.manage`

Roles seeded per org: Organization Owner (all), HR Manager, Payroll Manager, Department Manager, Employee.

## Pagination convention
Request: `?page=1&pageSize=20&search=&sortBy=&sortDesc=true|false`. Response: `{items, page, pageSize, totalCount, totalPages, hasNext, hasPrevious}`.
