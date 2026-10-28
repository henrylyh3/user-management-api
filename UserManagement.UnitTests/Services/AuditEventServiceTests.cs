using Moq;
using UserManagement.Application.Services;
using UserManagement.Core.DTOs;
using UserManagement.Core.Models;
using UserManagement.DataAccess.Interfaces;

namespace UserManagement.UnitTests.Services;

public sealed class AuditEventServiceTests
{
    [Fact]
    public async Task GetAsync_ReturnsSafePagedAuditEvents()
    {
        var query = new AuditEventQuery { Page = 2, PageSize = 10, Module = "Authentication" };
        var repository = new Mock<IAuditEventRepository>();
        repository.Setup(value => value.GetAsync(query, It.IsAny<CancellationToken>())).ReturnsAsync((new List<AuditEvent>
        {
            new() { Id = "event-id", OccurredOn = DateTime.UtcNow, Module = "Authentication", Action = "SignIn", Outcome = AuditOutcome.Success, ActorUserId = "68f7c7395492174899cb0123", IpAddress = "127.0.0.1", UserAgent = "private-agent", Metadata = new() { ["private"] = "value" } }
        }, 11));

        var result = await new AuditEventService(repository.Object).GetAsync(query);

        Assert.Single(result.Items);
        Assert.Equal(2, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.Equal(11, result.TotalCount);
        Assert.Equal("SignIn", result.Items[0].Action);
        Assert.DoesNotContain(typeof(AuditEventDto).GetProperties(), property => property.Name is nameof(AuditEvent.IpAddress) or nameof(AuditEvent.UserAgent) or nameof(AuditEvent.Metadata));
    }
}
