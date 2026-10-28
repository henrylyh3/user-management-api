namespace UserManagement.Core.DTOs;

public sealed class AuthSession
{
    public AuthResponse AccessToken { get; set; } = new();
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime RefreshTokenExpiresAt { get; set; }
    public bool KeepMeSignedIn { get; set; }
}
