# P0 — Foundation (delivered) & how to take it to P1

This note records exactly what P0 added, how to create the database, and what P1 will change. Pair it with `ARCHITECTURE-SSO-Master-v1.md`.

## What P0 added (this change)

New projects, all on `net8.0`, wired into `FundAdmin.slnx`; full solution builds clean:

```
SSO.Domain/         entities + enums (persistence-ignorant)
  Identity/         ApplicationUser, ApplicationRole, PasswordHistory
  Rbac/             Permission, RolePermission, UserPermission, Menu
  Auditing/         AuditEvent, [Auditable]
  Enums/            UserStatus, MfaType, PermissionOverrideType, AuditCategory/Outcome/Severity

SSO.Application/    abstractions
  Abstractions/     AuditContext, IAuditContextProvider

SSO.Infrastructure/ EF Core
  Persistence/      SsoDbContext (IdentityDbContext<…,Guid>), SsoDbContextFactory (design-time)
  Persistence/Configurations/   one IEntityTypeConfiguration per aggregate
  Auditing/         AuditSaveChangesInterceptor (audits [Auditable] changes in-transaction)
```

`Server.csproj` now references `SSO.Application` + `SSO.Infrastructure`. **No code in `Server/Program.cs` changed yet** — that is P1, so the running app is unaffected until you opt in.

## Create the database (you run this — it touches your SQL Server)

I did **not** run anything against your SQL Server. When ready, from `Project-SSO/`:

```powershell
# 1. Install the EF tool once (if not present)
dotnet tool install --global dotnet-ef

# 2. Point at your SSMS 2022 instance (adjust Server= to your instance name)
$env:SSO_CONNECTION = "Server=localhost;Database=SSO-PROJECT;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"

# 3. Generate the first migration (lives in SSO.Infrastructure/Migrations)
dotnet ef migrations add InitialIdentityRbacAudit `
  --project SSO.Infrastructure --startup-project SSO.Infrastructure

# 4. Review the generated migration, THEN apply it
dotnet ef database update `
  --project SSO.Infrastructure --startup-project SSO.Infrastructure
```

> `SsoDbContextFactory` lets EF tooling run against `SSO.Infrastructure` directly (no web host needed). Once P1 registers the context in `Server`, you can switch `--startup-project` to `Server` instead.

### Make `AuditEvents` tamper-evident (SQL Server 2022 Ledger)

EF Core 8 has no fluent API for ledger tables, so after generating the migration, hand-edit it to create the audit table as an **append-only ledger table**. In the generated `Up(...)`, change the `AuditEvents` `CreateTable` to include:

```csharp
// inside migrationBuilder.CreateTable(name: "AuditEvents", ...)
//   add to the table options:
//   .Annotation("SqlServer:IsTemporal", false)
// then, simplest reliable path — append raw SQL after the CreateTable:
migrationBuilder.Sql(@"
    -- Recreate AuditEvents as an append-only ledger table.
    -- (If the CreateTable already ran, drop & recreate, or author the table
    --  directly in SQL with WITH (LEDGER = ON (APPEND_ONLY = ON))).
");
```

The clean approach for a brand-new table is to **not** let EF create `AuditEvents`, and instead author it once via `migrationBuilder.Sql`:

```sql
CREATE TABLE dbo.AuditEvents (
    Id            BIGINT IDENTITY(1,1) PRIMARY KEY,
    OccurredAtUtc DATETIMEOFFSET   NOT NULL,
    Category      NVARCHAR(30)     NOT NULL,
    Action        NVARCHAR(100)    NOT NULL,
    Outcome       NVARCHAR(20)     NOT NULL,
    Severity      NVARCHAR(20)     NOT NULL,
    ActorUserId   UNIQUEIDENTIFIER NULL,
    ActorUserName NVARCHAR(256)    NULL,
    TargetType    NVARCHAR(200)    NULL,
    TargetId      NVARCHAR(256)    NULL,
    CorrelationId NVARCHAR(64)     NULL,
    IpAddress     NVARCHAR(64)     NULL,
    UserAgent     NVARCHAR(512)    NULL,
    DetailsJson   NVARCHAR(MAX)    NULL
)
WITH (LEDGER = ON (APPEND_ONLY = ON));
```

Verify later with `sys.database_ledger_transactions` and `sys.ledger_table_history`.

## What P1 will change (next session)

1. **Register persistence in `Server/Program.cs`:**
   - `AddDbContext<SsoDbContext>(… UseSqlServer(cs) … AddInterceptors(auditInterceptor))`
   - `AddIdentity<ApplicationUser, ApplicationRole>().AddEntityFrameworkStores<SsoDbContext>().AddDefaultTokenProviders()`
   - `IHttpContextAccessor` + a host `HttpContextAuditContextProvider : IAuditContextProvider`.
2. **Replace the in-memory IdP backing:** delete `Config.TestUsers` / `TestUserStore` usage; `Login.cshtml.cs` and `Mfa.cshtml.cs` call `SignInManager`/`UserManager`; per-user TOTP secret read from `ApplicationUser.MfaSecretEncrypted` (decrypted via Data Protection) instead of `Config.GetMfaSecret`.
3. **Move secrets out of source:** `Oidc:ClientSecret` and the connection string to user-secrets (dev) / environment (prod). Stop hard-coding in `Config.cs` / `appsettings.json`.
4. **Seed** a break-glass admin user, the base roles, the permission catalog, and the menu tree (idempotent seeder run at startup in Development).

## Acceptance criteria for P1 (security)

- A real user persisted in SQL Server logs in through the BFF with per-user TOTP.
- `TestUsers` removed; no credentials or secrets remain in source.
- Every change to an `[Auditable]` entity produces an `AuditEvents` row with actor + correlation id.
- Connection string and client secret resolved from configuration, not literals.
