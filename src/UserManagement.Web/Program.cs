using Serilog;
using UserManagement.Application;
using UserManagement.Core.Settings;
using UserManagement.Web.Exceptions;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using UserManagement.Application.Interfaces;
using UserManagement.Web.Services;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

var renderPort =
    Environment.GetEnvironmentVariable("PORT");

if (!string.IsNullOrWhiteSpace(renderPort))
{
    builder.WebHost.UseUrls(
        $"http://0.0.0.0:{renderPort}");
}

var logPath = Path.Combine(
    builder.Environment.ContentRootPath,
    "Logs",
    "log-.txt"
);

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            logPath,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            shared: true
        );
});
//controller
builder.Services.AddControllers();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Application + Repository DI
builder.Services.AddApplication();


//db
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDB")
);

//Cloudflare r2 setting
// File storage
var useR2 = string.Equals(builder.Configuration["FileStorage:Provider"], "CloudflareR2", StringComparison.OrdinalIgnoreCase);

if (useR2)
{
    builder.Services.Configure<CloudflareR2Settings>(builder.Configuration.GetSection("CloudflareR2"));

    builder.Services.AddSingleton<IAmazonS3>(services =>
    {
        var settings = services.GetRequiredService<IOptions<CloudflareR2Settings>>().Value;
        var credentials = new BasicAWSCredentials(settings.AccessKeyId, settings.SecretAccessKey);
        var config = new AmazonS3Config { ServiceURL = settings.S3Endpoint };

        return new AmazonS3Client(credentials, config);
    });

    builder.Services.AddScoped<IFileStorageService, CloudflareR2StorageService>();
}
else
{
    builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
}

builder.Services.AddProblemDetails();

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

//cors
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()
    ?? throw new InvalidOperationException(
        "CORS allowed origins configuration is missing.");

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

//health
builder.Services.AddHealthChecks();

//jwt section
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt")
);

var jwt = builder.Configuration
    .GetSection("Jwt")
    .Get<JwtSettings>()
    ?? throw new InvalidOperationException(
        "JWT configuration is missing."
    );

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;

        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,

                ValidIssuer = jwt.Issuer,
                ValidAudience = jwt.Audience,

                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwt.Key)
                ),

                RoleClaimType = "role",
                NameClaimType = ClaimTypes.Name,
                //reject token immediately after expiry
                ClockSkew = TimeSpan.Zero
            };
    });

builder.Services.AddAuthorization();


//current user
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
//app

var app = builder.Build();

app.Logger.LogInformation("File storage service: {StorageService}", useR2 ? nameof(CloudflareR2StorageService) : nameof(LocalFileStorageService));

app.UseExceptionHandler();
// Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseCors("Frontend");

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health");
app.Run();
