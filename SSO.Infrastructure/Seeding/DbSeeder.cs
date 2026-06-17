using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SSO.Application.Abstractions;
using SSO.Application.Authorization;
using SSO.Domain.Enums;
using SSO.Domain.Identity;
using SSO.Domain.Rbac;
using SSO.Infrastructure.Persistence;

namespace SSO.Infrastructure.Seeding;

/// <summary>
/// Idempotent seeding of the permission catalogue, base roles, the menu tree, and
/// a break-glass administrator. Safe to run on every startup: each step inserts
/// only what is missing.
/// </summary>
public sealed class DbSeeder
{
    private readonly SsoDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<ApplicationRole> _roles;
    private readonly IPasswordHistoryService _passwordHistory;

    public const string AdministratorRole = "Administrator";
    public const string InvestmentManagerRole = "InvestmentManager";

    public DbSeeder(
        SsoDbContext db,
        UserManager<ApplicationUser> users,
        RoleManager<ApplicationRole> roles,
        IPasswordHistoryService passwordHistory)
    {
        _db = db;
        _users = users;
        _roles = roles;
        _passwordHistory = passwordHistory;
    }

    public async Task SeedAsync(string adminEmail, string adminPassword, CancellationToken ct = default)
    {
        await SeedPermissionsAsync(ct);
        await SeedRolesAsync(ct);
        await SeedMenusAsync(ct);
        await SeedAdminAsync(adminEmail, adminPassword, ct);
    }

    private async Task SeedPermissionsAsync(CancellationToken ct)
    {
        var existing = await _db.Permissions.Select(p => p.Code).ToListAsync(ct);
        var missing = AppPermissions.All.Where(d => !existing.Contains(d.Code));

        foreach (var def in missing)
            _db.Permissions.Add(new Permission { Code = def.Code, Name = def.Name, Category = def.Category });

        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedRolesAsync(CancellationToken ct)
    {
        await EnsureRoleAsync(
            AdministratorRole, "Full administrative access", isSystem: true,
            AppPermissions.All.Select(p => p.Code).ToArray(), ct);

        await EnsureRoleAsync(
            InvestmentManagerRole, "Read access to dashboards, reports and master data", isSystem: false,
            new[]
            {
                AppPermissions.DashboardView,
                AppPermissions.ReportStockPositionView,
                AppPermissions.MasterSharesView,
                AppPermissions.MasterCoaView,
            }, ct);
    }

    private async Task EnsureRoleAsync(string name, string description, bool isSystem, string[] permissionCodes, CancellationToken ct)
    {
        var role = await _roles.FindByNameAsync(name);
        if (role is null)
        {
            role = new ApplicationRole(name) { Description = description, IsSystemRole = isSystem };
            var result = await _roles.CreateAsync(role);
            if (!result.Succeeded)
                throw new InvalidOperationException($"Failed to create role '{name}': {Errors(result)}");
        }

        var permissionIds = await _db.Permissions
            .Where(p => permissionCodes.Contains(p.Code))
            .Select(p => p.Id)
            .ToListAsync(ct);

        var alreadyGranted = await _db.RolePermissions
            .Where(rp => rp.RoleId == role.Id)
            .Select(rp => rp.PermissionId)
            .ToListAsync(ct);

        foreach (var permId in permissionIds.Except(alreadyGranted))
            _db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permId });

        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedMenusAsync(CancellationToken ct)
    {
        // (code, label, icon, route, sortOrder, parentCode, permissionCode)
        var defs = new (string Code, string Label, string? Icon, string? Route, int Sort, string? Parent, string? Perm)[]
        {
            ("dashboard", "Dashboard", "bi-speedometer2", "", 1, null, AppPermissions.DashboardView),

            ("report", "Report", "bi-folder", null, 10, null, null),
            ("report.stock-position", "Stock Position", "bi-file-earmark-bar-graph", "report/stock-position", 11, "report", AppPermissions.ReportStockPositionView),

            ("master-data", "Master Data", "bi-database", null, 20, null, null),
            ("master-data.shares", "Shares", "bi-building", "master-data/shares", 21, "master-data", AppPermissions.MasterSharesView),
            ("master-data.coa", "Chart of Account", "bi-journal-text", "master-data/chart-of-account", 22, "master-data", AppPermissions.MasterCoaView),

            ("administration", "Administration", "bi-shield-lock", null, 90, null, null),
            ("admin.users", "Users", "bi-people", "admin/users", 91, "administration", AppPermissions.AdminUsersManage),
            ("admin.roles", "Roles & Permissions", "bi-diagram-3", "admin/roles", 92, "administration", AppPermissions.AdminRolesManage),
            ("admin.audit", "Activity Log", "bi-clipboard-data", "admin/audit", 93, "administration", AppPermissions.AdminAuditView),
        };

        var permissionsByCode = await _db.Permissions.ToDictionaryAsync(p => p.Code, ct);
        var menusByCode = await _db.Menus.ToDictionaryAsync(m => m.Code, ct);

        // Process in sort order so a parent is always created before its children.
        foreach (var def in defs.OrderBy(d => d.Sort))
        {
            if (menusByCode.ContainsKey(def.Code))
                continue;

            var menu = new Menu
            {
                Code = def.Code,
                Label = def.Label,
                Icon = def.Icon,
                Route = def.Route,
                SortOrder = def.Sort,
                Parent = def.Parent is null ? null : menusByCode[def.Parent],
                RequiredPermission = def.Perm is null ? null : permissionsByCode[def.Perm],
            };

            _db.Menus.Add(menu);
            menusByCode[def.Code] = menu; // available as a parent for subsequent children
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedAdminAsync(string adminEmail, string adminPassword, CancellationToken ct)
    {
        if (await _users.FindByEmailAsync(adminEmail) is not null)
            return;

        var admin = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            FullName = "System Administrator",
            Status = UserStatus.Active,
            EmailConfirmed = true,
            MustChangePassword = true,           // force rotation of the bootstrap password
            PasswordChangedAtUtc = DateTimeOffset.UtcNow,
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };

        var result = await _users.CreateAsync(admin, adminPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException($"Failed to seed admin user: {Errors(result)}");

        await _users.AddToRoleAsync(admin, AdministratorRole);
        await _passwordHistory.RecordCurrentAsync(admin);
    }

    private static string Errors(IdentityResult result) =>
        string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"));
}
