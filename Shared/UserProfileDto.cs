namespace Shared;

/// <summary>
/// Data transfer object untuk profil pengguna yang diautentikasi.
/// </summary>
public class UserProfileDto
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
