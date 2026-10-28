using Moq;
using UserManagement.Application.Interfaces;
using UserManagement.Application.Services;
using UserManagement.Core.DTOs;
using UserManagement.Core.Models;
using UserManagement.DataAccess.Interfaces;

namespace UserManagement.UnitTests.Services;

public sealed class AdminUserServiceTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IRefreshTokenRepository> _refreshTokens = new();
    private readonly Mock<IAuditEventWriter> _audit = new();

    [Fact]
    public async Task ChangeRoleAsync_ValidPromotion_ChangesRoleAndRevokesSessions()
    {
        var user = new User { Id = "target-id", Role = UserRoles.User };
        _users.Setup(repository => repository.GetByIdAsync(user.Id)).ReturnsAsync(user);
        _users.Setup(repository => repository.ChangeRoleAsync(user.Id, UserRoles.Admin, It.IsAny<CancellationToken>())).ReturnsAsync(new User { Id = user.Id, Role = UserRoles.Admin });

        var result = await CreateService().ChangeRoleAsync("admin-id", user.Id, UserRoles.Admin);

        Assert.Equal(AdminUserMutationStatus.Success, result.Status);
        _refreshTokens.Verify(repository => repository.RevokeAllByUserIdAsync(user.Id), Times.Once);
        _audit.Verify(writer => writer.WriteAsync(It.Is<AuditEventRequest>(request => request.Action == "RoleChanged" && request.ActorUserId == "admin-id" && request.ResourceId == user.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChangeRoleAsync_SelfChange_ReturnsConflict()
    {
        var user = new User { Id = "admin-id", Role = UserRoles.Admin };
        _users.Setup(repository => repository.GetByIdAsync(user.Id)).ReturnsAsync(user);

        var result = await CreateService().ChangeRoleAsync(user.Id, user.Id, UserRoles.User);

        Assert.Equal(AdminUserMutationStatus.Conflict, result.Status);
        Assert.Equal("self_role_change", result.ConflictCode);
        _users.Verify(repository => repository.ChangeRoleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ChangeStatusAsync_DisableUser_DisablesAndRevokesSessions()
    {
        var user = new User { Id = "target-id", Role = UserRoles.User, Status = AccountStatus.Active };
        _users.Setup(repository => repository.GetByIdAsync(user.Id)).ReturnsAsync(user);
        _users.Setup(repository => repository.ChangeStatusAsync(user.Id, AccountStatus.Disabled, "admin-id", It.IsAny<CancellationToken>())).ReturnsAsync(new User { Id = user.Id, Status = AccountStatus.Disabled });

        var result = await CreateService().ChangeStatusAsync("admin-id", user.Id, AccountStatus.Disabled);

        Assert.Equal(AdminUserMutationStatus.Success, result.Status);
        _refreshTokens.Verify(repository => repository.RevokeAllByUserIdAsync(user.Id), Times.Once);
    }

    [Fact]
    public async Task ChangeStatusAsync_FinalActiveAdmin_ReturnsConflict()
    {
        var user = new User { Id = "target-id", Role = UserRoles.Admin, Status = AccountStatus.Active };
        _users.Setup(repository => repository.GetByIdAsync(user.Id)).ReturnsAsync(user);
        _users.Setup(repository => repository.CountActiveAdminsAsync()).ReturnsAsync(1);

        var result = await CreateService().ChangeStatusAsync("other-admin-id", user.Id, AccountStatus.Disabled);

        Assert.Equal(AdminUserMutationStatus.Conflict, result.Status);
        Assert.Equal("last_active_admin", result.ConflictCode);
        _refreshTokens.Verify(repository => repository.RevokeAllByUserIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UnlockAsync_ExistingUser_ResetsLockoutAndWritesAudit()
    {
        _users.Setup(repository => repository.UnlockAsync("target-id", It.IsAny<CancellationToken>())).ReturnsAsync(new User { Id = "target-id" });

        var result = await CreateService().UnlockAsync("admin-id", "target-id");

        Assert.Equal(AdminUserMutationStatus.Success, result.Status);
        _audit.Verify(writer => writer.WriteAsync(It.Is<AuditEventRequest>(request => request.Action == "AccountUnlocked" && request.ActorUserId == "admin-id"), It.IsAny<CancellationToken>()), Times.Once);
    }

    private AdminUserService CreateService() => new(_users.Object, _refreshTokens.Object, _audit.Object);
}
