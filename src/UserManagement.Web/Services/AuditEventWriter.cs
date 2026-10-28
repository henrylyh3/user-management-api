using System.Diagnostics;
using UserManagement.Application.Interfaces;
using UserManagement.Core.DTOs;
using UserManagement.Core.Models;
using UserManagement.DataAccess.Interfaces;

namespace UserManagement.Web.Services;

public sealed class AuditEventWriter : IAuditEventWriter
{
    private const int MaximumMetadataEntries = 10;
    private static readonly string[] SensitiveMetadataTerms = ["password", "token", "secret", "authorization", "cookie", "connectionstring", "credential", "apikey", "key"];
    private readonly IAuditEventRepository _repository;
    private readonly ICurrentUserService _currentUser;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditEventWriter> _logger;

    public AuditEventWriter(IAuditEventRepository repository, ICurrentUserService currentUser, IHttpContextAccessor httpContextAccessor, ILogger<AuditEventWriter> logger)
    {
        _repository = repository;
        _currentUser = currentUser;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task WriteAsync(AuditEventRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Module) || string.IsNullOrWhiteSpace(request.Action))
        {
            _logger.LogWarning("Audit event ignored because module or action was missing");
            return;
        }

        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var userAgent = httpContext?.Request.Headers.UserAgent.ToString();
            var auditEvent = new AuditEvent
            {
                OccurredOn = DateTime.UtcNow,
                Module = Truncate(request.Module.Trim(), 64),
                Action = Truncate(request.Action.Trim(), 100),
                Outcome = request.Outcome,
                ActorUserId = TruncateNullable(NullIfWhiteSpace(request.ActorUserId) ?? _currentUser.UserId, 128),
                ResourceType = TruncateNullable(request.ResourceType, 64),
                ResourceId = TruncateNullable(request.ResourceId, 128),
                IpAddress = TruncateNullable(httpContext?.Connection.RemoteIpAddress?.ToString(), 64),
                UserAgent = TruncateNullable(userAgent, 512),
                TraceId = TruncateNullable(Activity.Current?.TraceId.ToString() ?? httpContext?.TraceIdentifier, 128),
                Metadata = SanitizeMetadata(request.Metadata)
            };

            await _repository.CreateAsync(auditEvent, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError("Audit event persistence failed for module {Module}, action {Action}, exception type {ExceptionType}", Truncate(request.Module.Trim(), 64), Truncate(request.Action.Trim(), 100), exception.GetType().Name);
        }
    }

    private static Dictionary<string, string> SanitizeMetadata(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null)
        {
            return [];
        }

        var sanitized = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var entry in metadata.Where(entry => !string.IsNullOrWhiteSpace(entry.Key) && !ContainsSensitiveTerm(entry.Key)).Take(MaximumMetadataEntries))
        {
            sanitized.TryAdd(Truncate(entry.Key.Trim(), 64), Truncate(entry.Value ?? string.Empty, 256));
        }

        return sanitized;
    }

    private static bool ContainsSensitiveTerm(string key)
    {
        var normalizedKey = new string(key.Where(char.IsLetterOrDigit).ToArray());
        return SensitiveMetadataTerms.Any(term => normalizedKey.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static string? TruncateNullable(string? value, int maximumLength)
    {
        var normalized = NullIfWhiteSpace(value);
        return normalized is null ? null : Truncate(normalized, maximumLength);
    }

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Truncate(string value, int maximumLength) => value.Length <= maximumLength ? value : value[..maximumLength];
}
