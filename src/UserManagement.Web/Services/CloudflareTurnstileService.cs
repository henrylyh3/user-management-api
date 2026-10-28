using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using UserManagement.Application.Interfaces;
using UserManagement.Core.DTOs;
using UserManagement.Core.Models;
using UserManagement.Core.Settings;

namespace UserManagement.Web.Services;

public sealed class CloudflareTurnstileService : ICaptchaService
{
    private readonly HttpClient _httpClient;
    private readonly CaptchaSettings _settings;
    private readonly IAuditEventWriter _auditEventWriter;
    private readonly ILogger<CloudflareTurnstileService> _logger;

    public CloudflareTurnstileService(HttpClient httpClient, IOptions<CaptchaSettings> options, IAuditEventWriter auditEventWriter, ILogger<CloudflareTurnstileService> logger)
    {
        _httpClient = httpClient;
        _settings = options.Value;
        _auditEventWriter = auditEventWriter;
        _logger = logger;
    }

    public async Task<bool> ValidateAsync(string? token, string expectedAction, string? remoteIpAddress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 2048)
        {
            await RecordFailureAsync(expectedAction, "MissingOrInvalidToken", cancellationToken);
            return false;
        }

        var request = new TurnstileRequest
        {
            Secret = _settings.SecretKey,
            Response = token,
            RemoteIp = IPAddress.TryParse(remoteIpAddress, out var ipAddress) ? ipAddress.ToString() : null,
            IdempotencyKey = Guid.NewGuid().ToString()
        };

        try
        {
            using var response = await _httpClient.PostAsJsonAsync("siteverify", request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Turnstile validation returned HTTP status {StatusCode} for action {CaptchaAction}", (int)response.StatusCode, expectedAction);
                await RecordFailureAsync(expectedAction, "ProviderError", cancellationToken);
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<TurnstileResponse>(cancellationToken: cancellationToken);
            var valid = result is not null && result.Success && string.Equals(result.Action, expectedAction, StringComparison.Ordinal) && _settings.AllowedHostnames.Contains(result.Hostname ?? string.Empty, StringComparer.OrdinalIgnoreCase);

            if (valid) return true;

            var reason = result?.Success == true ? "ActionOrHostnameMismatch" : "ChallengeRejected";
            _logger.LogWarning("Turnstile validation failed for action {CaptchaAction} with reason {Reason} and error codes {ErrorCodes}", expectedAction, reason, result?.ErrorCodes ?? []);
            await RecordFailureAsync(expectedAction, reason, cancellationToken);
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Turnstile validation timed out for action {CaptchaAction}", expectedAction);
            await RecordFailureAsync(expectedAction, "ProviderTimeout", CancellationToken.None);
            return false;
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning("Turnstile validation request failed for action {CaptchaAction} with exception type {ExceptionType}", expectedAction, exception.GetType().Name);
            await RecordFailureAsync(expectedAction, "ProviderUnavailable", cancellationToken);
            return false;
        }
        catch (JsonException exception)
        {
            _logger.LogWarning("Turnstile validation returned an invalid response for action {CaptchaAction} with exception type {ExceptionType}", expectedAction, exception.GetType().Name);
            await RecordFailureAsync(expectedAction, "InvalidProviderResponse", cancellationToken);
            return false;
        }
    }

    private Task RecordFailureAsync(string expectedAction, string reason, CancellationToken cancellationToken)
    {
        return _auditEventWriter.WriteAsync(new AuditEventRequest { Module = "Security", Action = "CaptchaValidation", Outcome = AuditOutcome.Denied, Metadata = new Dictionary<string, string> { ["CaptchaAction"] = expectedAction, ["Reason"] = reason } }, cancellationToken);
    }

    private sealed class TurnstileRequest
    {
        [JsonPropertyName("secret")]
        public string Secret { get; init; } = string.Empty;

        [JsonPropertyName("response")]
        public string Response { get; init; } = string.Empty;

        [JsonPropertyName("remoteip")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? RemoteIp { get; init; }

        [JsonPropertyName("idempotency_key")]
        public string IdempotencyKey { get; init; } = string.Empty;
    }

    private sealed class TurnstileResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; init; }

        [JsonPropertyName("hostname")]
        public string? Hostname { get; init; }

        [JsonPropertyName("action")]
        public string? Action { get; init; }

        [JsonPropertyName("error-codes")]
        public string[] ErrorCodes { get; init; } = [];
    }
}
