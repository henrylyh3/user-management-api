namespace UserManagement.Core.DTOs;

public sealed class RevokeSessionResult
{
    public bool Revoked { get; init; }
    public bool RevokedCurrentSession { get; init; }
}
