# PeopleDesk — HR & Payroll SaaS Platform

A production-oriented, multi-tenant Human Resources and Payroll management platform for small and medium-sized businesses: employee records, attendance, leave, a configurable payroll engine, PDF payslips, employee self-service, notifications, analytics, and a SaaS platform administration console.

**Stack:** ASP.NET Core Web API (.NET 10) · React 19 + Vite + Tailwind CSS v4 · PostgreSQL · EF Core · Docker · GitHub Actions CI

---

## Highlights

- **Multi-tenancy with server-enforced isolation** — every tenant-owned table carries `OrganizationId`; EF Core global query filters bind to a per-request tenant context resolved from the JWT (platform admins can target a tenant via `X-Tenant-Id`). Cross-tenant reads return 404, never leaks.
- **Authentication** — JWT access tokens (15 min, memory-only on the client) + rotating refresh tokens in httpOnly cookies with **reuse detection** (replay revokes the whole session family), password lockout, email verification, password reset, optional TOTP two-factor auth.
- **Authorization** — ~30 granular permission codes checked by policy handlers on every endpoint *and* inside services; five seeded org roles (Owner, HR Manager, Payroll Manager, Department Manager, Employee).
- **Configurable payroll engine** — effective-dated, versioned `PayrollRuleSet` per jurisdiction (progressive brackets + pension rates). Ships with **Ethiopia** (Proclamation 1395/2025 + legacy 979/2016, pension 7%/11%). Gross→net from salary structures, attendance-derived overtime and unpaid leave, per-run adjustments. Draft → Calculated → Approved → Finalized (locked) → Reversed state machine with audit notes.
- **Leave ledger** — append-only `LeaveTransactions` with concurrency-checked `LeaveBalance` rows, overlap prevention, working-day-aware day counts (skips weekends/holidays), manager approval + audited HR override, calendar integration with attendance.
- **Attendance** — UTC storage, local-timezone calculations, late/early/overtime math, correction-request workflow, monthly summaries, finalized-period locking.
- **Payslips** — QuestPDF documents archived to disk, streamed through ACL-checked endpoints, publish + email notification.
- **Reliability** — transactional **email outbox** dispatched by a background worker (SMTP or dev `.eml` file sink). API calls never block on email.
- **Audit trail** — sensitive entities logged on every change with **values redacted** for passwords, bank accounts, national IDs and payroll amounts.
- **Reports & exports** — headcount, attendance trends, leave utilization, payroll expenses, turnover; CSV and Excel downloads.

## Repository layout

```
peopledesk/
├── backend/
│   ├── src/
│   │   ├── PeopleDesk.Domain/           # entities, enums, pure business rules (tax, attendance math)
│   │   ├── PeopleDesk.Application/      # use-case services, DTOs, FluentValidation, interfaces
│   │   ├── PeopleDesk.Infrastructure/   # EF Core, JWT, PDF, email outbox, seeders, migrations
│   │   └── PeopleDesk.Api/              # controllers, middleware, auth policy, OpenAPI
│   └── tests/
│       ├── PeopleDesk.UnitTests/        # tax schedules, attendance calculator, CSV
│       └── PeopleDesk.IntegrationTests/ # auth, tenant isolation, employee CRUD, leave, payroll (SQLite)
├── frontend/                            # React SPA (Vite, Tailwind, TanStack Query)
├── pgadmin/servers.json                 # pgAdmin pre-registered server (local PostgreSQL)
├── docs/api-contract.md                 # full REST contract (v1)
├── docker-compose.yml                   # postgres + pgadmin + api + web (nginx)
├── .github/workflows/ci.yml             # build + tests + docker images
└── .env.example
```

## Quick start (Docker — recommended)

```bash
cp .env.example .env
# set JWT__SECRET (openssl rand -base64 48) and the platform admin password
docker compose up --build
```

- Web UI: http://localhost (nginx proxies `/api` to the API container)
- API: http://localhost:8080 · Swagger UI: http://localhost:8080/swagger (dev) · health: http://localhost:8080/health

Migrations apply automatically at API startup; the permission catalog, subscription plans and the platform super admin are seeded idempotently.

## Quick start (local development, no Docker)

```bash
# Option A — PostgreSQL on localhost:5432
cd backend && dotnet ef database update --project src/PeopleDesk.Infrastructure --startup-project src/PeopleDesk.Api
dotnet run --project src/PeopleDesk.Api        # http://localhost:5090

# Option B — SQLite fallback (no database server needed)
ASPNETCORE_ENVIRONMENT=Development \
Database__Provider=sqlite \
Jwt__Secret="dev-secret-0123456789abcdef0123456789abcdef" \
dotnet run --project src/PeopleDesk.Api --urls http://localhost:5090
```

```bash
cd frontend && npm install && npm run dev      # http://localhost:5173 (proxies /api → 5090)
```

## Database administration (pgAdmin)

Browse and query the PeopleDesk database in a preconfigured pgAdmin instance:

```bash
docker compose up -d pgadmin
```

- URL: http://localhost:5050 — log in with `PGADMIN_DEFAULT_EMAIL` / `PGADMIN_DEFAULT_PASSWORD` (see `.env`, defaults `pgadmin@peopledesk.dev` / `pgadmin_dev_password`).
- The server **“PeopleDesk (local)”** is auto-registered on first start (`pgadmin/servers.json` → `host.docker.internal:5432`, maintenance DB `peopledesk`). It prompts for the database password once — use your `POSTGRES_PASSWORD`.
- The committed `servers.json` intentionally contains **no password**. To auto-connect, copy it to a private copy with a `"Password"` field, or just let pgAdmin remember the password in your browser session.

The API applies EF Core migrations and seeds the permission catalog, subscription plans, the platform super admin — and with `SEED_DEMO_DATA=true`, the “Acme Trading PLC” demo tenant — automatically at startup.

## Demo data (development only)

Start the API with `SEED_DEMO_DATA=true` to get a tenant **“Acme Trading PLC”** with seven employees, salary structures, attendance history, leave balances and a finalized payroll run:

| Email | Password | Role |
|---|---|---|
| owner@acme-demo.test | Demo#2026 | Organization Owner |
| hr@acme-demo.test | Demo#2026 | HR Manager |
| payroll@acme-demo.test | Demo#2026 | Payroll Manager |
| manager@acme-demo.test | Demo#2026 | Department Manager |
| employee@acme-demo.test | Demo#2026 | Employee |

Platform super admin (always seeded): configured via `Platform__SuperAdminEmail` / `Platform__SuperAdminPassword` (defaults `superadmin@peopledesk.local` / `ChangeMe_Platform#2026`) — **change it immediately**.

> ⚠️ Demo seeding is for development/evaluation only. Never enable it in production.

## Configuration

All settings are environment-driven (see [.env.example](.env.example)):

| Variable | Purpose |
|---|---|
| `ConnectionStrings__PostgreSQL` | Database connection (production uses PostgreSQL; SQLite is a dev fallback only) |
| `JWT__SECRET` | Signing key — **min 32 random chars, required** |
| `Auth__FrontendBaseUrl` | Composed into verification/reset/payslip email links |
| `Email__Mode` | `FileSink` (dev: writes `.eml` to `Email__SinkPath`) or `Smtp` |
| `Storage__Root` | File storage root (employee documents, payslips) |
| `SEED_DEMO_DATA` | `true` seeds demo tenant (dev only) |
| `PGADMIN_DEFAULT_EMAIL` / `PGADMIN_DEFAULT_PASSWORD` | pgAdmin UI login (http://localhost:5050 via `docker compose up -d pgadmin`) |

## Payroll rules — Ethiopia module

Rules are **data**, not code: `PayrollRuleSets` + `PayrollTaxBracket` rows, versioned and effective-dated. The engine selects the rule set whose effective range covers the pay period.

- **Current:** Proclamation **1395/2025** (`ET-2025-1395`, effective 2025-07) — monthly bands 0–2,000 (0%), 2,001–4,000 (15%), 4,001–7,000 (20%), 7,001–10,000 (25%), 10,001–14,000 (30%), >14,000 (35%); quick deductions precomputed so the shortcut method equals marginal-band summation.
- **Legacy:** Proclamation **979/2016** as amended by 1157/2019 (kept for historical effective-dated runs).
- **Pension:** 7% employee / 11% employer (Proclamation 746/2009).

> ⚠️ **Before production:** verify brackets, pension ceilings and the exact effective date against official sources (ERCA / Ministry of Revenue) for your jurisdiction. To change rules, seed a new `PayrollRuleSet` version — never modify engine code.

Sources consulted (2026-11): [EY Global Tax News](https://globaltaxnews.ey.com/news/2025-2542-ethiopia-issues-a-new-income-tax-proclamation), [Chambers & Partners analysis](https://chambers.com/articles/what-s-changed-under-ethiopia-s-new-income-tax-amendment-proclamation-no-1395-2025), [TaxDev](https://www.taxdev.org/news-events/ethiopias-revised-income-tax-explained).

## Testing

```bash
dotnet test backend/PeopleDesk.sln      # unit + integration (SQLite in temp files, no external deps)
cd frontend && npm test                 # if configured
```

Integration coverage: full auth cycle incl. refresh reuse revocation · cross-tenant 404 isolation · employee CRUD + permission denials + sensitive-field masking + subscription limits · leave reserve/approve/reject/cancel ledger math · payroll lifecycle with exact ET-2025 net-pay assertions · payslip access control · state-machine guards · reversal reopening.

## Security notes

- Access tokens live in memory only; refresh tokens are httpOnly cookies scoped to `/api/v1/auth` (SameSite=Lax, Secure in prod). The SPA always calls same-origin `/api` (Vite proxy in dev, nginx in prod) — no cross-site cookie surface.
- Rate limiting: 15 req/min/IP on auth endpoints, 300 req/min/IP globally (429 with `Retry-After` semantics).
- File uploads: extension allow-list + 10 MB cap + path-traversal-safe storage keys.
- Audit logs redact salary/bank/national-ID/secret values. Serilog file logs exclude SQL payloads (enable `DebugSql=true` only for troubleshooting).
- Passwords: ASP.NET Core Identity PBKDF2 (v3 format) with rehash-on-login.
- Run behind TLS in production (nginx `X-Forwarded-Proto` detection already honors it).

## Deployment & operations

**Production deployment**

```bash
# 1. Provision a server with Docker + Compose
# 2. Clone the repo, copy .env.example → .env and fill secrets (JWT, DB password, SMTP, admin password)
# 3. Bring the stack up
docker compose up -d --build
# 4. Verify
curl -f http://localhost:8080/health
```

Recommended hardening: terminate TLS at a front proxy (Caddy/TrafficManager/nginx) with certificates; restrict `POSTGRES` port to the private network (remove the `ports:` mapping on the postgres service); set `ASPNETCORE_ENVIRONMENT=Production`.

**Database backup & restore**

```bash
# Backup (nightly cron recommended)
docker exec peopledesk-postgres pg_dump -U peopledesk -Fc peopledesk > backup_$(date +%F).dump

# Restore
docker exec -i peopledesk-postgres pg_restore -U peopledesk -d peopledesk --clean < backup_2026-11-04.dump

# File storage (documents + payslips) — back up the api-data volume:
docker run --rm -v peopledesk_api-data:/data -v $PWD:/backup alpine tar czf /backup/files.tgz /data
```

**Data retention:** audit logs, payslip archives and employee documents are retained indefinitely by default; implement a retention job appropriate to your jurisdiction before production rollout.

**Logs:** structured console output + rolling file logs (`logs/peopledesk-*.log`, 30-day retention) with request IP, actor and error codes — never payloads or salaries.

## API documentation

Full endpoint reference: [docs/api-contract.md](docs/api-contract.md). Live OpenAPI document at `/openapi/v1.json` with Swagger UI at `/swagger` in Development.

## License notes

QuestPDF is used under the **Community license** (free for organizations < USD 1M annual revenue) — review before commercial deployment. All other dependencies are MIT/Apache-licensed.
