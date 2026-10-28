namespace UserManagement.Core.DTOs;

public enum SigninStatus
{
    Success,
    InvalidCredentials,
    CaptchaRequired,
    EmailVerificationRequired,
    LockedOut,
    AccountDisabled
}

public sealed class SigninResult
{
    public SigninStatus Status { get; init; }
    public AuthSession? Session { get; init; }
    public bool CaptchaRequired { get; init; }
}
