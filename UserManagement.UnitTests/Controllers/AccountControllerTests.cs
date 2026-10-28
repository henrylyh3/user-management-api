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

public sealed class AccountControllerTests
{
    [Fact]
    public async Task ForgotPassword_InvalidCaptcha_DoesNotRequestReset()
    {
        var accountService = new Mock<IAccountService>();
        var captchaService = new Mock<ICaptchaService>();
        captchaService.Setup(service => service.ValidateAsync("invalid-token", "forgot_password", It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var controller = CreateController(accountService, captchaService);

        var result = await controller.ForgotPassword(new ForgotPasswordRequest { Email = "user@example.com", CaptchaToken = "invalid-token" }, CancellationToken.None);

        Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, ((ObjectResult)result).StatusCode);
        accountService.Verify(service => service.RequestPasswordResetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Signup_DoesNotValidateCaptcha()
    {
        var accountService = new Mock<IAccountService>();
        var captchaService = new Mock<ICaptchaService>();
        accountService.Setup(service => service.SignupAsync(It.IsAny<SignupRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        var controller = CreateController(accountService, captchaService);

        var result = await controller.Signup(new SignupRequest { FirstName = "Henry", LastName = "Lee", Email = "user@example.com", Password = "password123" }, CancellationToken.None);

        Assert.IsType<AcceptedResult>(result);
        captchaService.Verify(service => service.ValidateAsync(It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static AccountController CreateController(Mock<IAccountService> accountService, Mock<ICaptchaService> captchaService)
    {
        var environment = new Mock<IWebHostEnvironment>();
        environment.SetupGet(value => value.EnvironmentName).Returns(Environments.Development);
        return new AccountController(accountService.Object, Mock.Of<ICurrentUserService>(), captchaService.Object, Mock.Of<IAntiforgery>(), Options.Create(new RefreshTokenSettings()), Mock.Of<ILogger<AccountController>>(), environment.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }
}
