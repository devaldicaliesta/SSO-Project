namespace SSO.Application.Authorization;

/// <summary>
/// The canonical catalogue of permission codes. These are the stable contracts
/// checked by <c>[Authorize(Policy = "perm:...")]</c> on the API and used to gate
/// menu visibility. The seeder inserts <see cref="All"/> into the database; code
/// references the <c>const</c> fields so a typo is a compile error, not a silent
/// authorization hole.
/// </summary>
public static class AppPermissions
{
    public const string DashboardView = "dashboard.view";

    public const string ReportStockPositionView = "report.stock-position.view";

    public const string MasterSharesView = "master-data.shares.view";
    public const string MasterSharesManage = "master-data.shares.manage";
    public const string MasterCoaView = "master-data.coa.view";
    public const string MasterCoaManage = "master-data.coa.manage";

    public const string AdminUsersManage = "admin.users.manage";
    public const string AdminRolesManage = "admin.roles.manage";
    public const string AdminAuditView = "admin.audit.view";

    public sealed record Definition(string Code, string Name, string Category);

    /// <summary>Full catalogue with display metadata, consumed by the seeder.</summary>
    public static readonly IReadOnlyList<Definition> All = new[]
    {
        new Definition(DashboardView, "View dashboard", "General"),
        new Definition(ReportStockPositionView, "View stock position report", "Reports"),
        new Definition(MasterSharesView, "View shares", "Master Data"),
        new Definition(MasterSharesManage, "Manage shares", "Master Data"),
        new Definition(MasterCoaView, "View chart of accounts", "Master Data"),
        new Definition(MasterCoaManage, "Manage chart of accounts", "Master Data"),
        new Definition(AdminUsersManage, "Manage users", "Administration"),
        new Definition(AdminRolesManage, "Manage roles & permissions", "Administration"),
        new Definition(AdminAuditView, "View activity / audit log", "Administration"),
    };
}
