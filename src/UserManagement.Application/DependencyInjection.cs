using Microsoft.Extensions.DependencyInjection;
using UserManagement.Application.Interfaces;
using UserManagement.Application.Services;
using UserManagement.DataAccess.Interfaces;
using UserManagement.DataAccess.Repositories;
using Microsoft.AspNetCore.Identity;
using UserManagement.Core.Models;

namespace UserManagement.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        services.AddScoped<IUserService, UserService>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();

        services.AddScoped<IPasswordHasher<User>,PasswordHasher<User>>();
        return services;
    }
}