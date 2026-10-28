using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using UserManagement.Application.Interfaces;
using UserManagement.Core.DTOs;
using UserManagement.Core.Settings;
using UserManagement.Web.Controllers;

namespace UserManagement.UnitTests.Controllers;

public class AuthControllerTests
{
    [Fact]
    public async Task Signin_Development_SetsHttpCompatibleRefreshCookie()
    {
        var controller = CreateController(Environments.Development, keepMeSignedIn: false);

        await controller.Signin(new SigninRequest { Email = "user@example.com", Password = "password" });

        var cookie = controller.Response.Headers.SetCookie.ToString();

        Assert.Contains("refresh_token=", cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("expires=", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Signin_Production_SetsSecurePersistentRefreshCookie()
    {
        var controller = CreateController(Environments.Production, keepMeSignedIn: true);

        await controller.Signin(new SigninRequest { Email = "user@example.com", Password = "password", KeepMeSignedIn = true });

        var cookie = controller.Response.Headers.SetCookie.ToString();

        Assert.Contains("refresh_token=", cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=none", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expires=", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Signin_CaptchaRequired_ReturnsBadRequestWithCaptchaFlag()
    {
        var controller = CreateController(Environments.Development, keepMeSignedIn: false, SigninStatus.CaptchaRequired);

        var result = await controller.Signin(new SigninRequest { Email = "user@example.com", Password = "password" });

        var response = Assert.IsType<ObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(response.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Equal(true, problem.Extensions["captchaRequired"]);
        Assert.Equal("captcha_required", problem.Extensions["code"]);
    }

    private static AuthController CreateController(string environmentName, bool keepMeSignedIn, SigninStatus status = SigninStatus.Success)
    {
        var authService = new Mock<IAuthService>();
        var environment = new Mock<IWebHostEnvironment>();
        var expiresAt = DateTime.UtcNow.AddDays(7);

        authService
            .Setup(service => service.SigninAsync(It.IsAny<SigninRequest>(), It.IsAny<ClientSessionInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SigninResult { Status = status, CaptchaRequired = status == SigninStatus.CaptchaRequired, Session = new AuthSession
            {
                AccessToken = new AuthResponse { Token = "access-token", ExpiresAt = DateTime.UtcNow.AddHours(1) },
                RefreshToken = "refresh-token",
                RefreshTokenExpiresAt = expiresAt,
                KeepMeSignedIn = keepMeSignedIn
            }});
        environment.SetupGet(value => value.EnvironmentName).Returns(environmentName);

        var controller = new AuthController(
            authService.Object,
            Mock.Of<ICurrentUserService>(),
            Mock.Of<ILogger<AuthController>>(),
            Mock.Of<IAntiforgery>(),
            Options.Create(new RefreshTokenSettings()),
            environment.Object
        )
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        return controller;
    }
}
