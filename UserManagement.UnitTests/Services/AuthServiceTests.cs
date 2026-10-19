using Microsoft.AspNetCore.Identity;
using Moq;
using UserManagement.Application.Interfaces;
using UserManagement.Application.Services;
using UserManagement.Core.DTOs;
using UserManagement.Core.Models;
using UserManagement.DataAccess.Interfaces;

namespace UserManagement.UnitTests.Services;

public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _repository = new();
    private readonly Mock<IPasswordHasher<User>> _passwordHasher = new();
    private readonly Mock<IJwtTokenService> _tokenService = new();

    private AuthService CreateService()
    {
        return new AuthService(_repository.Object, _passwordHasher.Object, _tokenService.Object);
    }

    // POST /auth/signup
    [Fact]
    public async Task SignupAsync_EmailAlreadyExists_ReturnsNull()
    {
        var existingUser = new User { Email = "user@example.com" };
        var request = new SignupRequest { FirstName = "Henry", LastName = "Lee", Email = " USER@EXAMPLE.COM ", Password = "password123" };

        _repository.Setup(repository => repository.GetByEmailAsync("user@example.com")).ReturnsAsync(existingUser);

        var result = await CreateService().SignupAsync(request);

        Assert.Null(result);
    }

    // POST /auth/signup
    [Fact]
    public async Task SignupAsync_NewUser_CreatesUserAndReturnsToken()
    {
        var request = new SignupRequest { FirstName = " Henry ", LastName = " Lee ", Email = " HENRY@EXAMPLE.COM ", Password = "password123" };
        var expectedResponse = new AuthResponse { Token = "jwt-token", ExpiresAt = DateTime.UtcNow.AddHours(1) };

        _repository.Setup(repository => repository.GetByEmailAsync("henry@example.com")).ReturnsAsync((User?)null);
        _passwordHasher.Setup(hasher => hasher.HashPassword(It.IsAny<User>(), "password123")).Returns("hashed-password");
        _tokenService.Setup(service => service.CreateToken(It.IsAny<User>())).Returns(expectedResponse);

        var result = await CreateService().SignupAsync(request);

        Assert.Same(expectedResponse, result);
        _repository.Verify(repository => repository.CreateAsync(It.IsAny<User>()), Times.Once);
    }

    // POST /auth/signin
    [Fact]
    public async Task SigninAsync_IncorrectEmailOrPassword_ReturnsNull()
    {
        var request = new SigninRequest { Email = "wrong@example.com", Password = "wrong-password" };

        _repository.Setup(repository => repository.GetByEmailAsync("wrong@example.com")).ReturnsAsync((User?)null);

        var result = await CreateService().SigninAsync(request);

        Assert.Null(result);
    }

    // POST /auth/signin
    [Fact]
    public async Task SigninAsync_ValidPassword_ReturnsToken()
    {
        var user = new User { Id = "user-id", Email = "user@example.com", PasswordHash = "stored-hash", Role = "User" };
        var request = new SigninRequest { Email = " USER@EXAMPLE.COM ", Password = "password123" };
        var expectedResponse = new AuthResponse { Token = "jwt-token", ExpiresAt = DateTime.UtcNow.AddHours(1) };

        _repository.Setup(repository => repository.GetByEmailAsync("user@example.com")).ReturnsAsync(user);
        _passwordHasher.Setup(hasher => hasher.VerifyHashedPassword(user, "stored-hash", "password123")).Returns(PasswordVerificationResult.Success);
        _tokenService.Setup(service => service.CreateToken(user)).Returns(expectedResponse);

        var result = await CreateService().SigninAsync(request);

        Assert.Same(expectedResponse, result);
    }
}
