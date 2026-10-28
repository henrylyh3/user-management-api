namespace UserManagement.Application.Interfaces;

public interface ICurrentUserService
{
    string? UserId { get; }
    string? Email { get; }
    string? Role { get; }
    string? SessionId { get; }
    bool IsAuthenticated { get; }
    bool IsAdmin { get; }
}
