using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using UserManagement.Application.Interfaces;
using UserManagement.Core.DTOs;
using UserManagement.Core.Models;
using UserManagement.DataAccess.Interfaces;
using UserManagement.Web.Services;

namespace UserManagement.UnitTests.Services;

public sealed class AuditEventWriterTests
{
    [Fact]
    public async Task WriteAsync_ValidEvent_AddsRequestContextAndFiltersSensitiveMetadata()
    {
        AuditEvent? storedEvent = null;
        var repository = new Mock<IAuditEventRepository>();
        var currentUser = new Mock<ICurrentUserService>();
        var httpContext = new DefaultHttpContext { TraceIdentifier = "trace-id" };
        httpContext.Connection.RemoteIpAddress = IPAddress.Loopback;
        httpContext.Request.Headers.UserAgent = "Unit test browser";
        currentUser.SetupGet(service => service.UserId).Returns("current-user-id");
        repository.Setup(value => value.CreateAsync(It.IsAny<AuditEvent>(), It.IsAny<CancellationToken>())).Callback<AuditEvent, CancellationToken>((auditEvent, _) => storedEvent = auditEvent).Returns(Task.CompletedTask);
        var writer = new AuditEventWriter(repository.Object, currentUser.Object, new HttpContextAccessor { HttpContext = httpContext }, Mock.Of<ILogger<AuditEventWriter>>());

        await writer.WriteAsync(new AuditEventRequest
        {
            Module = "Orders",
            Action = "OrderCreated",
            Outcome = AuditOutcome.Success,
            ResourceType = "Order",
            ResourceId = "order-id",
            Metadata = new Dictionary<string, string> { ["Plan"] = "Free", ["Access-Token"] = "must-not-be-stored", ["Password_Hint"] = "must-not-be-stored" }
        });

        Assert.NotNull(storedEvent);
        Assert.Equal("current-user-id", storedEvent.ActorUserId);
        Assert.Equal(IPAddress.Loopback.ToString(), storedEvent.IpAddress);
        Assert.Equal("Unit test browser", storedEvent.UserAgent);
        Assert.Equal("trace-id", storedEvent.TraceId);
        Assert.Equal("Free", storedEvent.Metadata["Plan"]);
        Assert.DoesNotContain("Access-Token", storedEvent.Metadata.Keys);
        Assert.DoesNotContain("Password_Hint", storedEvent.Metadata.Keys);
    }

    [Fact]
    public async Task WriteAsync_ExplicitActor_OverridesCurrentUser()
    {
        AuditEvent? storedEvent = null;
        var repository = new Mock<IAuditEventRepository>();
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(service => service.UserId).Returns("current-user-id");
        repository.Setup(value => value.CreateAsync(It.IsAny<AuditEvent>(), It.IsAny<CancellationToken>())).Callback<AuditEvent, CancellationToken>((auditEvent, _) => storedEvent = auditEvent).Returns(Task.CompletedTask);
        var writer = new AuditEventWriter(repository.Object, currentUser.Object, new HttpContextAccessor(), Mock.Of<ILogger<AuditEventWriter>>());

        await writer.WriteAsync(new AuditEventRequest { Module = "Authentication", Action = "SignOut", Outcome = AuditOutcome.Success, ActorUserId = "token-owner-id" });

        Assert.Equal("token-owner-id", storedEvent?.ActorUserId);
    }

    [Fact]
    public async Task WriteAsync_StorageFailure_DoesNotFailBusinessOperation()
    {
        var repository = new Mock<IAuditEventRepository>();
        repository.Setup(value => value.CreateAsync(It.IsAny<AuditEvent>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("database unavailable"));
        var writer = new AuditEventWriter(repository.Object, Mock.Of<ICurrentUserService>(), new HttpContextAccessor(), Mock.Of<ILogger<AuditEventWriter>>());

        var exception = await Record.ExceptionAsync(() => writer.WriteAsync(new AuditEventRequest { Module = "Account", Action = "PasswordChange", Outcome = AuditOutcome.Success }));

        Assert.Null(exception);
    }
}
