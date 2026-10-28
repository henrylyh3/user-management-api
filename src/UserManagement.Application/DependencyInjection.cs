using Microsoft.Extensions.DependencyInjection;
using UserManagement.Application.Interfaces;
using UserManagement.Application.Services;
using UserManagement.DataAccess.Interfaces;
using UserManagement.DataAccess.Repositories;
using Microsoft.AspNetCore.Identity;
using UserManagement.Core.Models;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UserManagement.DataAccess.Database;

namespace UserManagement.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<IAuditEventService, AuditEventService>();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IAccountTokenRepository, AccountTokenRepository>();
        services.AddScoped<IAuditEventRepository, AuditEventRepository>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.TryAddScoped<IAuditActorProvider, SystemAuditActorProvider>();

        services.AddScoped<IPasswordHasher<User>,PasswordHasher<User>>();
        return services;
    }
}
