using UserManagement.Core.DTOs;
using UserManagement.Core.Models;

namespace UserManagement.Application.Interfaces;

public interface IJwtTokenService
{
    AuthResponse CreateToken(User user);
}