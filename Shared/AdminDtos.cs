namespace Shared;

/// <summary>A user row in the admin console list.</summary>
public class UserListItemDto
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool LockedOut { get; set; }
    public bool MfaEnrolled { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTimeOffset? LastLoginAtUtc { get; set; }
    public List<string> Roles { get; set; } = new();
}

public class CreateUserRequest
{
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    /// <summary>Optional. When omitted a temporary password is generated and returned once.</summary>
    public string? Password { get; set; }
    public List<string> Roles { get; set; } = new();
}

public class SetUserRolesRequest
{
    public List<string> Roles { get; set; } = new();
}

/// <summary>Returned once when an admin creates a user or resets a password.</summary>
public class TemporaryPasswordDto
{
    public Guid UserId { get; set; }
    public string TemporaryPassword { get; set; } = string.Empty;
}

public class RoleDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }
    public List<string> Permissions { get; set; } = new();
}

public class CreateRoleRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public List<string> Permissions { get; set; } = new();
}

public class SetRolePermissionsRequest
{
    public List<string> Permissions { get; set; } = new();
}

public class PermissionDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
}
