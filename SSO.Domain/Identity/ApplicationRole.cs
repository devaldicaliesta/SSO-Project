using Microsoft.AspNetCore.Identity;
using SSO.Domain.Auditing;
using SSO.Domain.Rbac;

namespace SSO.Domain.Identity;

/// <summary>
/// A role is a named bundle of <see cref="Permission"/>s. Users are assigned
/// roles (via Identity's join table); the effective permission set is resolved
/// from the union of role permissions plus per-user overrides.
/// </summary>
[Auditable]
public class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole() { }

    public ApplicationRole(string roleName) : base(roleName) { }

    /// <summary>Human-readable purpose of the role (shown in the admin console).</summary>
    public string? Description { get; set; }

    /// <summary>
    /// System roles (e.g. the break-glass admin) are seeded and protected from
    /// deletion/rename in the admin UI.
    /// </summary>
    public bool IsSystemRole { get; set; }

    /// <summary>The permissions granted by this role.</summary>
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
