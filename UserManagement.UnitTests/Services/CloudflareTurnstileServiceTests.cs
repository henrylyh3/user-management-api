using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using UserManagement.Application.Interfaces;
using UserManagement.Core.DTOs;
using UserManagement.Core.Settings;
using UserManagement.Web.Services;

namespace UserManagement.UnitTests.Services;

public sealed class CloudflareTurnstileServiceTests
{
    [Fact]
    public async Task ValidateAsync_MatchingActionAndHostname_ReturnsTrue()
    {
        var service = CreateService(HttpStatusCode.OK, """{"success":true,"hostname":"localhost","action":"forgot_password","error-codes":[]}""");

        var result = await service.ValidateAsync("captcha-token", "forgot_password", "127.0.0.1");

        Assert.True(result);
    }

    [Fact]
    public async Task ValidateAsync_MismatchedAction_ReturnsFalseAndWritesAuditEvent()
    {
        var audit = new Mock<IAuditEventWriter>();
        var service = CreateService(HttpStatusCode.OK, """{"success":true,"hostname":"localhost","action":"signin","error-codes":[]}""", audit);

        var result = await service.ValidateAsync("captcha-token", "forgot_password", "127.0.0.1");

        Assert.False(result);
        audit.Verify(writer => writer.WriteAsync(It.Is<AuditEventRequest>(request => request.Module == "Security" && request.Action == "CaptchaValidation"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ValidateAsync_MissingToken_ReturnsFalseWithoutCallingProvider()
    {
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{}");
        var service = CreateService(handler);

        var result = await service.ValidateAsync(null, "signin", "127.0.0.1");

        Assert.False(result);
        Assert.Equal(0, handler.RequestCount);
    }

    private static CloudflareTurnstileService CreateService(HttpStatusCode statusCode, string response, Mock<IAuditEventWriter>? audit = null)
    {
        return CreateService(new StubHttpMessageHandler(statusCode, response), audit);
    }

    private static CloudflareTurnstileService CreateService(StubHttpMessageHandler handler, Mock<IAuditEventWriter>? audit = null)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://challenges.cloudflare.com/turnstile/v0/") };
        var settings = Options.Create(new CaptchaSettings { SecretKey = "test-secret", AllowedHostnames = ["localhost"], SigninRequiredAfterFailedAttempts = 2 });
        return new CloudflareTurnstileService(client, settings, (audit ?? new Mock<IAuditEventWriter>()).Object, Mock.Of<ILogger<CloudflareTurnstileService>>());
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _response;

        public StubHttpMessageHandler(HttpStatusCode statusCode, string response)
        {
            _statusCode = statusCode;
            _response = response;
        }

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(_statusCode) { Content = new StringContent(_response, Encoding.UTF8, "application/json") });
        }
    }
}
