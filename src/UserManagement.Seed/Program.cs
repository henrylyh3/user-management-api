using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using UserManagement.Core.Models;
using UserManagement.Core.Settings;
using UserManagement.DataAccess.Repositories;
using UserManagement.DataAccess.Database;
using UserManagement.DataAccess.Interfaces;

var builder = Host.CreateApplicationBuilder(
    new HostApplicationBuilderSettings
    {
        Args = args,
        ContentRootPath = AppContext.BaseDirectory
    }
);

builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDB")
);

builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<IAuditActorProvider, SystemAuditActorProvider>();

builder.Services.AddScoped<
    IPasswordHasher<User>,
    PasswordHasher<User>
>();

using var host = builder.Build();
using var scope = host.Services.CreateScope();

var repository =
    scope.ServiceProvider.GetRequiredService<UserRepository>();

var passwordHasher =
    scope.ServiceProvider
        .GetRequiredService<IPasswordHasher<User>>();

await SeedUserAsync(
    repository,
    passwordHasher,
    firstName: "user",
    lastName: "user",
    email: "user@user.com",
    password: "user",
    role: UserRoles.User
);

await SeedUserAsync(
    repository,
    passwordHasher,
    firstName: "admin",
    lastName: "admin",
    email: "admin@admin.com",
    password: "admin",
    role: UserRoles.Admin
);

Console.WriteLine("User seeding completed.");

static async Task SeedUserAsync(
    UserRepository repository,
    IPasswordHasher<User> passwordHasher,
    string firstName,
    string lastName,
    string email,
    string password,
    string role)
{
    var normalizedEmail =
        email.Trim().ToLowerInvariant();

    var existingUser =
        await repository.GetByEmailAsync(normalizedEmail);

    if (existingUser is not null)
    {
        Console.WriteLine(
            $"Skipped '{email}': user already exists."
        );

        return;
    }

    var user = new User
    {
        FirstName = firstName,
        LastName = lastName,
        Email = normalizedEmail,
        Role = role,
        IsEmailVerified = true
    };

    user.PasswordHash =
        passwordHasher.HashPassword(
            user,
            password
        );

    await repository.CreateAsync(user);

    Console.WriteLine(
        $"Created '{email}' with role '{role}'."
    );
}
