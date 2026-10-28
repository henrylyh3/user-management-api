using Microsoft.AspNetCore.Authorization;
using UserManagement.Core.Models;
using UserManagement.Web.Controllers;

namespace UserManagement.UnitTests.Controllers;

public sealed class AdminUsersControllerTests
{
    [Fact]
    public void Controller_RequiresAdminRole()
    {
        var authorize = Assert.Single(typeof(AdminUsersController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal(UserRoles.Admin, authorize.Roles);
    }
}
