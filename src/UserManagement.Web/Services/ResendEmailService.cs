using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using UserManagement.Application.Interfaces;
using UserManagement.Core.Settings;

namespace UserManagement.Web.Services;

public sealed class ResendEmailService : IEmailService
{
    private readonly HttpClient _httpClient;
    private readonly ResendSettings _settings;
    private readonly ILogger<ResendEmailService> _logger;

    public ResendEmailService(HttpClient httpClient, IOptions<ResendSettings> options, ILogger<ResendEmailService> logger)
    {
        _httpClient = httpClient;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(string recipientEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString("N"));
        request.Content = JsonContent.Create(new { from = $"{_settings.FromName} <{_settings.FromEmail}>", to = new[] { recipientEmail }, subject, html = htmlBody });
        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Email provider returned HTTP {(int)response.StatusCode}.");
        }

        _logger.LogInformation("Account email accepted by provider");
    }
}
