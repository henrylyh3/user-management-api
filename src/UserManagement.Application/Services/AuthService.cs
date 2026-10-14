using Microsoft.AspNetCore.Identity;
using UserManagement.Application.Interfaces;
using UserManagement.Core.DTOs;
using UserManagement.Core.Models;
using UserManagement.DataAccess.Interfaces;

namespace UserManagement.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _repository;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IJwtTokenService _tokenService;

    public AuthService(
        IUserRepository repository,
        IPasswordHasher<User> passwordHasher,
        IJwtTokenService tokenService)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    public async Task<AuthResponse?> SignupAsync(
        SignupRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (await _repository.GetByEmailAsync(email) is not null)
        {
            return null;
        }

        var user = new User
        {
            Name = request.Name.Trim(),
            Email = email,
            Role = "User",
            CreatedOn = DateTime.UtcNow,
            UpdatedOn = DateTime.UtcNow
        };

        user.PasswordHash = _passwordHasher.HashPassword(
            user,
            request.Password
        );

        await _repository.CreateAsync(user);

        return _tokenService.CreateToken(user);
    }

    public async Task<AuthResponse?> SigninAsync(
        SigninRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _repository.GetByEmailAsync(email);

        if (user is null)
        {
            return null;
        }

        var result = _passwordHasher.VerifyHashedPassword(
            user,
            user.PasswordHash,
            request.Password
        );

        if (result == PasswordVerificationResult.Failed)
        {
            return null;
        }

        return _tokenService.CreateToken(user);
    }
}