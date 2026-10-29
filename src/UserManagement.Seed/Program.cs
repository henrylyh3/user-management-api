using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using UserManagement.Core.Models;
using UserManagement.Core.Settings;
using UserManagement.DataAccess.Database;
using UserManagement.DataAccess.Interfaces;
using UserManagement.DataAccess.Repositories;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

builder.Services.Configure<MongoDbSettings>(builder.Configuration.GetSection("MongoDB"));
builder.Services.AddScoped<UserRepository>();
builder.Services.AddScoped<IAuditActorProvider, SystemAuditActorProvider>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

using var host = builder.Build();
using var scope = host.Services.CreateScope();
var repository = scope.ServiceProvider.GetRequiredService<UserRepository>();
var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
var settings = scope.ServiceProvider.GetRequiredService<IOptions<MongoDbSettings>>().Value;
var users = new MongoClient(settings.ConnectionString).GetDatabase(settings.DatabaseName).GetCollection<BsonDocument>("users");

await SeedUserAsync(repository, users, passwordHasher, "user", "user", "user@user.com", "user", UserRoles.User);
await SeedUserAsync(repository, users, passwordHasher, "admin", "admin", "admin@admin.com", "admin", UserRoles.Admin);

Console.WriteLine("User seeding completed.");

static async Task SeedUserAsync(UserRepository repository, IMongoCollection<BsonDocument> users, IPasswordHasher<User> passwordHasher, string firstName, string lastName, string email, string password, string role)
{
    var normalizedEmail = email.Trim().ToLowerInvariant();
    // Read raw BSON so legacy fields do not prevent the missing-field backfill.
    var existing = await users.Find(new BsonDocument("Email", normalizedEmail)).FirstOrDefaultAsync();
    var now = DateTime.UtcNow;
    var user = new User
    {
        Id = ObjectId.GenerateNewId().ToString(),
        FirstName = firstName,
        LastName = lastName,
        Email = normalizedEmail,
        Role = role,
        Status = AccountStatus.Active,
        IsEmailVerified = true,
        CreatedOn = now,
        ModifiedOn = now,
        CreatedBy = AuditActors.System,
        ModifiedBy = AuditActors.System
    };

    if (existing is null || !existing.Contains("PasswordHash"))
    {
        user.PasswordHash = passwordHasher.HashPassword(user, password);
    }

    if (existing is null)
    {
        await repository.CreateAsync(user);
        Console.WriteLine($"Created '{normalizedEmail}' with role '{role}'.");
        return;
    }

    // Derive defaults from the current model, including nullable security fields.
    // Existing values (including nulls, passwords, roles and status) are preserved.
    var defaults = user.ToBsonDocument();
    defaults.Remove("_id");
    var missing = new BsonDocument(defaults.Elements.Where(field => !existing.Contains(field.Name)));
    if (missing.ElementCount == 0)
    {
        Console.WriteLine($"Skipped '{normalizedEmail}': schema already current.");
        return;
    }

    var count = missing.ElementCount;
    missing["ModifiedOn"] = now;
    missing["ModifiedBy"] = AuditActors.System;
    // Guard each field against a concurrent write; never replace the whole user.
    var updates = missing.Elements.Select(field => new UpdateOneModel<BsonDocument>(
        new BsonDocument { { "_id", existing["_id"] }, { field.Name, new BsonDocument("$exists", false) } },
        new BsonDocument("$set", new BsonDocument(field.Name, field.Value)))).ToList();
    // Audit stamps describe the system backfill even when those columns exist.
    updates.Add(new UpdateOneModel<BsonDocument>(new BsonDocument("_id", existing["_id"]), new BsonDocument("$set", new BsonDocument { { "ModifiedOn", now }, { "ModifiedBy", AuditActors.System } })));
    await users.BulkWriteAsync(updates);
    Console.WriteLine($"Updated '{normalizedEmail}': backfilled {count} missing fields.");
}
