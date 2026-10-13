using Microsoft.Extensions.DependencyInjection;
using UserManagement.Application.Interfaces;
using UserManagement.Application.Services;
using UserManagement.DataAccess.Interfaces;
using UserManagement.DataAccess.Repositories;

namespace UserManagement.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        services.AddScoped<IUserService, UserService>();

        services.AddScoped<IUserRepository, UserRepository>();

        return services;
    }
}