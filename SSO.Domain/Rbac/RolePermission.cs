using SSO.Domain.Auditing;
using SSO.Domain.Identity;

namespace SSO.Domain.Rbac;

/// <summary>Join entity: which <see cref="Permission"/>s a role grants.</summary>
[Auditable]
public class RolePermission
{
    public Guid RoleId { get; set; }
    public ApplicationRole? Role { get; set; }

    public int PermissionId { get; set; }
    public Permission? Permission { get; set; }
}
