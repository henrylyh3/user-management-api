using Serilog;
using Serilog.Context;
using UserManagement.Application;
using UserManagement.Core.Settings;
using UserManagement.Web.Exceptions;
using UserManagement.Web.Health;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using UserManagement.Application.Interfaces;
using UserManagement.Web.Services;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Options;
using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography.X509Certificates;
using UserManagement.DataAccess.Database;
using UserManagement.DataAccess.Interfaces;
using OpenTelemetry.Metrics;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry;
using System.Diagnostics;

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
builder.Services.AddScoped<IAuditActorProvider, HttpAuditActorProvider>();
builder.Services.AddSingleton<MongoIndexInitializer>();


//db
builder.Services.AddOptions<MongoDbSettings>()
    .Bind(builder.Configuration.GetSection("MongoDB"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var mongoSettings = builder.Configuration.GetSection("MongoDB").Get<MongoDbSettings>() ?? throw new InvalidOperationException("MongoDB configuration is missing.");
var dataProtectionBuilder = builder.Services.AddDataProtection()
    .SetApplicationName("UserManagement.Web")
    .AddKeyManagementOptions(options => options.XmlRepository = new MongoDataProtectionKeyRepository(mongoSettings));

if (!builder.Environment.IsDevelopment())
{
    var dataProtectionSettings = builder.Configuration.GetSection("DataProtection").Get<DataProtectionSettings>() ?? throw new InvalidOperationException("Data Protection configuration is missing.");

    if (string.IsNullOrWhiteSpace(dataProtectionSettings.CertificateBase64) || string.IsNullOrWhiteSpace(dataProtectionSettings.CertificatePassword))
    {
        throw new InvalidOperationException("Data Protection certificate configuration is missing.");
    }

    var certificate = X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(dataProtectionSettings.CertificateBase64), dataProtectionSettings.CertificatePassword, X509KeyStorageFlags.EphemeralKeySet, null);
    dataProtectionBuilder.ProtectKeysWithCertificate(certificate);
}

builder.Services.AddOptions<RefreshTokenSettings>()
    .Bind(builder.Configuration.GetSection("RefreshToken"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<AccountSecuritySettings>()
    .Bind(builder.Configuration.GetSection("AccountSecurity"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<AuditSettings>()
    .Bind(builder.Configuration.GetSection("Audit"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var maximumFailedSigninAttempts = builder.Configuration.GetValue<int>("AccountSecurity:MaxFailedSigninAttempts");

builder.Services.AddOptions<CaptchaSettings>()
    .Bind(builder.Configuration.GetSection("Captcha"))
    .ValidateDataAnnotations()
    .Validate(settings => settings.AllowedHostnames.All(hostname => !string.IsNullOrWhiteSpace(hostname) && !hostname.Contains('/') && !hostname.Contains(':')), "Captcha allowed hostnames must contain hostnames only.")
    .Validate(settings => settings.SigninRequiredAfterFailedAttempts < maximumFailedSigninAttempts, "Captcha must be required before the account lockout threshold.")
    .ValidateOnStart();

var emailProvider = builder.Configuration["Email:Provider"] ?? throw new InvalidOperationException("Email provider configuration is missing.");

if (!string.Equals(emailProvider, "Resend", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException("Email provider must be Resend.");
}

builder.Services.AddOptions<ResendSettings>()
    .Bind(builder.Configuration.GetSection("Resend"))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHttpClient<IEmailService, ResendEmailService>(client =>
{
    client.BaseAddress = new Uri("https://api.resend.com/");
    client.Timeout = TimeSpan.FromSeconds(30);
}).AddResilienceHandler("resend-retry", pipeline => pipeline.AddRetry(new HttpRetryStrategyOptions { MaxRetryAttempts = 3, Delay = TimeSpan.FromSeconds(1), BackoffType = Polly.DelayBackoffType.Exponential, UseJitter = true }));

builder.Services.AddHttpClient<ICaptchaService, CloudflareTurnstileService>(client =>
{
    client.BaseAddress = new Uri("https://challenges.cloudflare.com/turnstile/v0/");
    client.Timeout = TimeSpan.FromSeconds(10);
}).AddResilienceHandler("turnstile-retry", pipeline => pipeline.AddRetry(new HttpRetryStrategyOptions { MaxRetryAttempts = 2, Delay = TimeSpan.FromMilliseconds(250), BackoffType = Polly.DelayBackoffType.Exponential, UseJitter = true }));

//Cloudflare r2 setting
// File storage
var storageProvider = builder.Configuration["FileStorage:Provider"] ?? throw new InvalidOperationException("File storage provider configuration is missing.");

if (!string.Equals(storageProvider, "Local", StringComparison.OrdinalIgnoreCase) && !string.Equals(storageProvider, "CloudflareR2", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException("File storage provider must be Local or CloudflareR2.");
}

var useR2 = string.Equals(storageProvider, "CloudflareR2", StringComparison.OrdinalIgnoreCase);

if (useR2)
{
    builder.Services.AddOptions<CloudflareR2Settings>()
        .Bind(builder.Configuration.GetSection("CloudflareR2"))
        .ValidateDataAnnotations()
        .ValidateOnStart();

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

var knownProxyIps = builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [];
var trustCloudflareConnectingIp = builder.Configuration.GetValue<bool>("RateLimiting:TrustCloudflareConnectingIp");

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    foreach (var proxyIp in knownProxyIps)
    {
        if (!IPAddress.TryParse(proxyIp, out var parsedProxyIp))
        {
            throw new InvalidOperationException("Forwarded headers contains an invalid known proxy IP.");
        }

        options.KnownProxies.Add(parsedProxyIp);
    }
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var useSecureCookies = !builder.Environment.IsDevelopment();

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-Token";
    options.Cookie.Name = useSecureCookies ? "__Host-user-management-antiforgery" : "user-management-antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.Path = "/";
    options.Cookie.SameSite = useSecureCookies ? SameSiteMode.None : SameSiteMode.Lax;
    options.Cookie.SecurePolicy = useSecureCookies ? CookieSecurePolicy.Always : CookieSecurePolicy.SameAsRequest;
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("Auth", httpContext =>
    {
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var cloudflareClientIp = httpContext.Request.Headers["CF-Connecting-IP"].FirstOrDefault();

        if (trustCloudflareConnectingIp && IPAddress.TryParse(cloudflareClientIp, out var parsedCloudflareClientIp))
        {
            clientIp = parsedCloudflareClientIp.ToString();
        }

        return RateLimitPartition.GetFixedWindowLimiter(
            clientIp,
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 5,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            });
    });
});

//health
builder.Services.AddHealthChecks().AddCheck<MongoHealthCheck>("mongodb");

var openTelemetry = builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("UserManagement.Api"))
    .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation())
    .WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddRuntimeInstrumentation());

if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
{
    openTelemetry.UseOtlpExporter();
}

builder.Services.AddOptions<JwtSettings>()
    .Bind(builder.Configuration.GetSection("Jwt"))
    .ValidateDataAnnotations()
    .Validate(settings => !string.IsNullOrWhiteSpace(settings.Issuer) && !string.IsNullOrWhiteSpace(settings.Audience) && Encoding.UTF8.GetByteCount(settings.Key) >= 32, "Jwt Issuer, Audience, and a Key of at least 32 bytes are required.")
    .ValidateOnStart();

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

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var userId = context.Principal?.FindFirstValue("sub");
                var tokenVersionClaim = context.Principal?.FindFirstValue("ver");
                var sessionId = context.Principal?.FindFirstValue("sid");

                if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(sessionId) || !int.TryParse(tokenVersionClaim, out var tokenVersion))
                {
                    context.Fail("Invalid token claims.");
                    return;
                }

                var repository = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
                var refreshTokenRepository = context.HttpContext.RequestServices.GetRequiredService<IRefreshTokenRepository>();
                var user = await repository.GetByIdAsync(userId);

                if (user is null || user.Status == UserManagement.Core.Models.AccountStatus.Disabled || user.TokenVersion != tokenVersion || !await refreshTokenRepository.IsSessionActiveAsync(userId, sessionId))
                {
                    context.Fail("Token has been revoked.");
                }
            }
        };
    });

builder.Services.AddAuthorization();


//current user
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IAuditEventWriter, AuditEventWriter>();
//app

var app = builder.Build();

_ = app.Services.GetRequiredService<IOptions<MongoDbSettings>>().Value;
_ = app.Services.GetRequiredService<IOptions<AccountSecuritySettings>>().Value;
_ = app.Services.GetRequiredService<IOptions<AuditSettings>>().Value;
_ = app.Services.GetRequiredService<IOptions<CaptchaSettings>>().Value;
_ = app.Services.GetRequiredService<IOptions<ResendSettings>>().Value;

if (useR2)
{
    _ = app.Services.GetRequiredService<IOptions<CloudflareR2Settings>>().Value;
}

await app.Services.GetRequiredService<MongoIndexInitializer>().ReconcileAsync();
app.Logger.LogInformation("MongoDB index schema version {IndexSchemaVersion} initialized", MongoIndexInitializer.SchemaVersion);

app.Logger.LogInformation("File storage service: {StorageService}", useR2 ? nameof(CloudflareR2StorageService) : nameof(LocalFileStorageService));

app.UseForwardedHeaders();
app.UseExceptionHandler();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.Use(async (context, next) =>
{
    var traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
    context.Response.Headers.TryAdd("X-Trace-Id", traceId);
    context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
    context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
    context.Response.Headers.TryAdd("Referrer-Policy", "no-referrer");
    context.Response.Headers.TryAdd("Permissions-Policy", "camera=(), geolocation=(), microphone=()");
    using (LogContext.PushProperty("TraceId", traceId))
    {
        await next();
    }
});
// Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging(options => options.EnrichDiagnosticContext = (diagnosticContext, httpContext) => diagnosticContext.Set("TraceId", Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier));
app.UseCors("Frontend");
app.UseRateLimiter();

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health");
app.Run();
