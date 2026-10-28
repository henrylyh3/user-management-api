using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Moq;
using UserManagement.Application.Interfaces;
using UserManagement.Core.DTOs;
using UserManagement.Web.Controllers;

namespace UserManagement.UnitTests.Controllers;

public sealed class AuditEventsControllerTests
{
    [Fact]
    public void Controller_RequiresAdminRole()
    {
        var authorize = Assert.Single(typeof(AuditEventsController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());

        Assert.Equal("Admin", authorize.Roles);
    }

    [Fact]
    public async Task Get_ReturnsPagedEvents()
    {
        var query = new AuditEventQuery();
        var expected = new PagedResult<AuditEventDto> { Items = [new AuditEventDto { Id = "event-id" }], Page = 1, PageSize = 20, TotalCount = 1 };
        var service = new Mock<IAuditEventService>();
        service.Setup(value => value.GetAsync(query, It.IsAny<CancellationToken>())).ReturnsAsync(expected);

        var result = await new AuditEventsController(service.Object).Get(query, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(expected, ok.Value);
    }
}
