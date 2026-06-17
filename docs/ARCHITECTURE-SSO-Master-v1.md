# SSO Master Platform — Architecture & Refactor Blueprint (v1)

> Target: refactor the existing FundAdmin BFF skeleton into a **centralized master SSO platform** with comprehensive user/password management, permission-based RBAC for menus, and tamper-evident activity monitoring — at enterprise security level for an OJK-supervised multifinance.
>
> **Locked decisions** (from kickoff): Identity = **Duende IdentityServer + ASP.NET Core Identity**; Database = **SQL Server 2022 (SSMS)**; Runtime = **.NET 8 (`net8.0`)**; Frontend pattern = **BFF + Blazor WASM (retained)**.
>
> This document is the companion to `files/ArsitekturFinal-AssetManagement-v5-ZeroTrust.md`. The v5 doc describes the *whole* Asset Management platform (Keycloak, k3s, mesh, etc.) as the long-horizon infrastructure vision. **This document scopes only the SSO/identity tier** and deliberately keeps it on the .NET stack the team already owns.

---

## 1. Where we are vs. where we are going

### 1.1 What exists today (verified, not assumed)

| Area | Current state | Verdict |
|---|---|---|
| Solution | `Client` (Blazor WASM) + `Server` (host) + `Shared`, .NET 8 | Keep, evolve |
| Auth flow | Duende BFF 3.1 + IdentityServer 7, OAuth2 **code + PKCE**, tokens server-side, `__Host-` HttpOnly/Secure/SameSite=Strict cookie | **Sound — keep** |
| MFA | TOTP implemented (`TotpService`, `Mfa.cshtml.cs`), `amr=[pwd,mfa]` | Keep, move secret to per-user store |
| User store | **1 hardcoded `TestUser`**, plaintext dev password, in-memory | **Replace** |
| RBAC | a single `role` claim string | **Replace with permission model** |
| Activity audit | none | **Build** |
| Secrets | client secret + TOTP secret hardcoded in `Config.cs`/`appsettings.json` | **Externalize** |
| Persistence | none (everything in-memory) | **Add EF Core + SQL Server 2022** |

The BFF/PKCE/cookie foundation is genuinely correct and matches v5 §5.1. We build **on** it, we do not rip it out.

### 1.2 The shape of the refactor in one sentence

> Add a **persistent Identity + Master tier** (SQL-backed user store, permission-based RBAC, tamper-evident audit) *behind* the existing BFF, and replace the in-memory `TestUsers`/`role`-claim model with it.

---

## 2. Target solution structure (Clean-ish layering)

We introduce three class libraries between `Shared` and `Server`, giving a dependency flow with **no cycles** and a domain core that knows nothing about EF/ASP.NET.

```
Project-SSO/
├─ FundAdmin.slnx
├─ docs/
│   └─ ARCHITECTURE-SSO-Master-v1.md        (this file)
├─ Shared/            DTOs/contracts shared with the WASM client (no server deps)
├─ Client/            Blazor WASM SPA (unchanged pattern; gains menu/permission UI)
├─ SSO.Domain/        Entities + enums + domain invariants. Depends on: Identity.Stores only.
├─ SSO.Application/    Use-cases, service interfaces, RBAC resolution, audit abstractions, DTO mapping.
├─ SSO.Infrastructure/ EF Core DbContext (Identity+RBAC+Audit), configs, migrations,
│                      IdentityServer persisted stores, audit sink, security services.
└─ Server/            Host: IdentityServer + BFF + API controllers + middleware + composition root.
```

**Reference direction (compile-time):**

```
Domain      → (Microsoft.Extensions.Identity.Stores)
Application → Domain, Shared
Infrastructure → Application, Domain
Server      → Infrastructure, Application, Domain, Shared, Client
Client      → Shared
```

Why this shape:
- **Domain** is pure: entities + rules, no DB/web. Lets us unit-test RBAC resolution and password-policy logic without a database.
- **Application** holds the *what* (interfaces, use-cases, `IAuditService`, `IPermissionResolver`); **Infrastructure** holds the *how* (EF Core, SQL, IdentityServer stores). Swapping SQL Server later (or adding Keycloak per v5) touches Infrastructure only.
- **Server** stays a thin composition root + transport (controllers, Razor pages, middleware).

> We are **adding** projects, not renaming `Client`/`Server`/`Shared`, to keep the build green and the diff reviewable.

---

## 3. Identity & authentication design

### 3.1 ASP.NET Core Identity as the credential store

Replace `Config.TestUsers` and `TestUserStore` with **ASP.NET Core Identity** over EF Core:

- `ApplicationUser : IdentityUser<Guid>` and `ApplicationRole : IdentityRole<Guid>` (GUID keys — non-enumerable, safe to expose).
- Identity already gives us, battle-tested: **PBKDF2 password hashing**, `LockoutEnd` / `AccessFailedCount`, `TwoFactorEnabled`, `SecurityStamp` (session invalidation on credential change), email/phone confirmation, normalized lookups.
- IdentityServer's interactive login (`Login.cshtml.cs` / `Mfa.cshtml.cs`) calls `SignInManager`/`UserManager` instead of `TestUserStore`.

### 3.2 Password management (NIST 800-63B aligned, OJK-defensible)

Configured via `IdentityOptions` + custom validators/services:

| Control | Mechanism |
|---|---|
| Strength policy | `PasswordOptions` (length ≥ 12, complexity), plus a **breached-password / banned-list validator** (custom `IPasswordValidator`) |
| Password history | `PasswordHistory` table; custom validator rejects last N hashes |
| Max age / forced change | `PasswordChangedAtUtc` on user + `MustChangePassword` flag; checked at login → redirect to change-password |
| Lockout | Identity lockout: `MaxFailedAccessAttempts`, `DefaultLockoutTimeSpan` (adaptive lockout = v5 §5.2) |
| Reset | Token-based reset (`UserManager.GeneratePasswordResetTokenAsync`) — never email the password |
| First-login provisioning | Admin creates user with a one-time temp password + `MustChangePassword=true` |

### 3.3 MFA per user (replace the shared dev secret)

- TOTP secret moves from the hardcoded `MfaSecrets` dict to a **per-user encrypted column** (`MfaSecretEncrypted`), generated at enrollment. Encrypt with the ASP.NET Core **Data Protection API** (keys themselves go to a protected store / later HSM per v5 §6.3).
- Keep the existing `TotpService` (RFC 6238) for generation/verification.
- **FIDO2/WebAuthn** for privileged accounts is a later phase (v5 §5.3) — the schema reserves space (`MfaType`).
- Step-up (`acr=mfa-high`) for sensitive operations is a later phase but the audit + policy hooks are designed in now.

### 3.4 Session & token posture (unchanged, already correct)

Keep BFF server-side sessions, `__Host-` cookie, antiforgery header, PKCE. Add only: shorten cookie lifetime to a reviewed value and bind permission claims into the session (see §4.4).

---

## 4. RBAC — permission-based menu authorization

A single `role` string cannot express menu-level control. We move to **permission-based RBAC** (roles are bundles of permissions; menus and API endpoints are gated by *permissions*, not roles). This is the model OJK auditors expect ("who can do what, and who approved it").

### 4.1 Data model

```
ApplicationUser ──< UserRole >── ApplicationRole ──< RolePermission >── Permission
       │                                                                   │
       └──────────────< UserPermission (grant/deny override) >─────────────┘
                                                                            │
                                            Menu ── RequiredPermissionId ───┘  (nullable)
                                            Menu.ParentId → Menu (self-ref tree)
```

| Entity | Key fields | Purpose |
|---|---|---|
| `Permission` | `Id`, `Code` (e.g. `shares.read`, `coa.write`), `Name`, `Category` | Atomic authorization unit |
| `RolePermission` | `RoleId`, `PermissionId` | Role = bundle of permissions |
| `UserPermission` | `UserId`, `PermissionId`, `Type` (Grant/Deny) | Per-user override; **Deny wins** |
| `Menu` | `Id`, `ParentId`, `Code`, `Label`, `Icon`, `Route`, `SortOrder`, `RequiredPermissionId` | Hierarchical menu; visibility gated by a permission |

`UserRole` = Identity's built-in `IdentityUserRole<Guid>`.

### 4.2 Permission resolution (the one algorithm to get right)

```
effective(user) = ( ∪ permissions of user's roles  ∪  user Grant overrides )
                  \  user Deny overrides
```

Implemented in `IPermissionResolver` in **Application** (pure, unit-testable). Result is a `HashSet<string>` of permission codes.

### 4.3 Enforcement at three layers (defense in depth, v5 §2)

1. **API** — permission policies via a dynamic `IAuthorizationPolicyProvider`: `[Authorize(Policy = "perm:shares.read")]`. No need to predefine every policy; the provider parses the `perm:` prefix.
2. **Menu / UI** — `GET /api/menu` returns only the menu subtree whose `RequiredPermission` the user holds; the Blazor `NavMenu` renders the server-resolved tree (no hardcoded links). Pages also carry `[Authorize(Policy="perm:...")]` equivalents so a deep-link can't bypass a hidden menu.
3. **Data** — (later) SQL Server **Row-Level Security** per company/branch (v5 §6.4).

### 4.4 Where permissions live at runtime

- On login, the resolved permission codes are written as `permission` **claims into the BFF session** (server-side, not the browser) so each request avoids a DB round-trip.
- Cache invalidation: bumping the user's `SecurityStamp` (on any role/permission change) forces re-resolution on next validation. A short server cache (e.g. 5 min) backs the resolver.

---

## 5. Activity monitoring — tamper-evident audit

### 5.1 What we capture

| Category | Examples |
|---|---|
| Authentication | Login success/failure, MFA success/failure, lockout, logout, password reset |
| Authorization | Permission denied (403), step-up required/satisfied |
| User management | User created/disabled, role assigned/revoked, permission granted/denied, password changed |
| Data change | Create/Update/Delete of audited entities (who, when, before/after) |
| Security | Suspicious patterns (later → fraud engine, v5 §6.10) |

### 5.2 `AuditEvent` schema (SQL Server 2022 **Ledger table** — tamper-evident, v5 §6.4)

| Column | Notes |
|---|---|
| `Id` | bigint identity |
| `OccurredAtUtc` | server UTC |
| `Category`, `Action` | e.g. `Authentication` / `Login.Failed` |
| `Outcome`, `Severity` | Success/Failure; Info/Warning/Critical |
| `ActorUserId`, `ActorUserName` | who (nullable for anonymous attempts) |
| `TargetType`, `TargetId` | what was acted on |
| `CorrelationId` | ties events of one request together |
| `IpAddress`, `UserAgent` | from the request |
| `DetailsJson` | structured extra data (no secrets/PII in clear) |

Created as an **append-only updatable ledger table** so any tampering is cryptographically detectable and provable to an OJK auditor.

### 5.3 How events are captured (no scattered logging)

- **Authentication**: in the login/MFA pages + an IdentityServer `IEventSink`.
- **Authorization denials**: a custom `IAuthorizationMiddlewareResultHandler` / middleware.
- **Data changes**: an EF Core **`SaveChangesInterceptor`** that diffs tracked entities marked `[Auditable]`.
- **Explicit sensitive actions**: `IAuditService.RecordAsync(...)` calls in use-cases.
- **SIEM feed** (v5 §5.5): Serilog structured logs → file/Seq now, Wazuh later; the Ledger table is the durable source of truth.

---

## 6. Security hardening checklist (mapped to v5 & regulation)

| Control | This platform | v5 / reg ref |
|---|---|---|
| Tokens never in browser | BFF server-side sessions (kept) | §5.1 |
| CSRF | BFF antiforgery header (kept) | §5.1 |
| Secrets externalized | `appsettings` → user-secrets (dev) → env/secret store (prod); **no secrets in source** | §6.3 |
| Password hashing | Identity PBKDF2 (upgradeable) | §5.2 |
| Adaptive lockout + brute-force | Identity lockout + rate limiting middleware | §5.2, §8 |
| MFA | Per-user TOTP; FIDO2 for privileged (later) | §5.3 |
| Security headers | CSP, HSTS, X-Content-Type-Options, Referrer-Policy via middleware | §5.1 |
| Audit tamper-evidence | SQL Ledger table | §6.4 |
| PII protection | Always Encrypted / column encryption for sensitive PII (later) | §6.4, UU PDP |
| Least privilege DB | Dedicated SQL login, not `sa`; later dynamic secrets | §5.8 |
| SoD / recertification | Role/permission model enables it; IGA process later | §6.7 |
| Data residency | DB + backups in Indonesia | §3 |

---

## 7. Phased roadmap (security acceptance criteria per phase)

| Phase | Scope | Done when… |
|---|---|---|
| **P0 — Foundation** *(this PR)* | `SSO.Domain` + `SSO.Infrastructure` (DbContext, Identity+RBAC+Audit entities/configs, audit interceptor). Solution builds. | `dotnet build` green; schema modeled; **no secrets in source** |
| **P1 — Persistent identity** | EF migration applied to SQL Server 2022; IdentityServer + login/MFA use `UserManager`/`SignInManager`; seed admin + roles + permissions | A real user in SQL can log in via BFF with TOTP; `TestUsers` deleted |
| **P2 — Password management** | Policy, history, lockout, forced change, reset flow, per-user encrypted TOTP | Weak/breached/reused passwords rejected; lockout works; reset works |
| **P3 — RBAC menus** | `IPermissionResolver`, dynamic permission policies, `/api/menu`, server-driven `NavMenu` | Menu/API access strictly follow permissions; deep-link bypass blocked |
| **P4 — Activity monitoring** | Audit on auth/authz/user-mgmt/data-change; admin audit viewer; Serilog→Seq | Every sensitive action audited in Ledger; tamper check passes |
| **P5 — Admin console** | User/role/permission/menu CRUD UI (admin-gated) | Admin can manage the full identity lifecycle |
| **P6 — Hardening** | Security headers, rate limiting, secret store, pen-test pass, step-up auth | No critical findings; step-up on sensitive ops |

---

## 8. Open items to confirm during P1

- Branch/company scoping: is menu/data access also scoped per branch (multi-tenant RLS)? Affects `UserRole`/`UserPermission` (scope column).
- Admin bootstrap: seed a break-glass admin via config, or first-run setup wizard?
- Audit retention period (regulatory — typically ≥5–10 years for OJK).

---

*v1 — living document. Update as phases land.*
