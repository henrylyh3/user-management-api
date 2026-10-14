using UserManagement.Core.DTOs;

namespace UserManagement.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse?> SignupAsync(SignupRequest request);
    Task<AuthResponse?> SigninAsync(SigninRequest request);
}