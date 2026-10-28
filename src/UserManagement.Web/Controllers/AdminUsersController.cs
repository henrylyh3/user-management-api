using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UserManagement.Application.Interfaces;
using UserManagement.Core.DTOs;
using UserManagement.Core.Models;
using UserManagement.Web.Validation;

namespace UserManagement.Web.Controllers;

[ApiController]
[Authorize(Roles = UserRoles.Admin)]
[Route("api/admin/users")]
public sealed class AdminUsersController(IAdminUserService adminUserService, ICurrentUserService currentUser, IAntiforgery antiforgery) : ControllerBase
{
    [HttpPatch("{id}/role")]
    public async Task<IActionResult> ChangeRole([MongoObjectId] string id, ChangeUserRoleRequest request, CancellationToken cancellationToken)
    {
        if (!await IsCsrfRequestValidAsync()) return InvalidCsrf();
        if (string.IsNullOrWhiteSpace(currentUser.UserId)) return UnauthorizedProblem();

        var result = await adminUserService.ChangeRoleAsync(currentUser.UserId, id, request.Role, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> ChangeStatus([MongoObjectId] string id, ChangeUserStatusRequest request, CancellationToken cancellationToken)
    {
        if (!await IsCsrfRequestValidAsync()) return InvalidCsrf();
        if (string.IsNullOrWhiteSpace(currentUser.UserId)) return UnauthorizedProblem();

        var status = Enum.Parse<AccountStatus>(request.Status, ignoreCase: false);
        var result = await adminUserService.ChangeStatusAsync(currentUser.UserId, id, status, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{id}/unlock")]
    public async Task<IActionResult> Unlock([MongoObjectId] string id, CancellationToken cancellationToken)
    {
        if (!await IsCsrfRequestValidAsync()) return InvalidCsrf();
        if (string.IsNullOrWhiteSpace(currentUser.UserId)) return UnauthorizedProblem();

        var result = await adminUserService.UnlockAsync(currentUser.UserId, id, cancellationToken);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult(AdminUserMutationResult result)
    {
        return result.Status switch
        {
            AdminUserMutationStatus.Success => NoContent(),
            AdminUserMutationStatus.NotFound => Problem(statusCode: StatusCodes.Status404NotFound, title: "User not found", detail: "The requested user does not exist."),
            AdminUserMutationStatus.Conflict => Problem(statusCode: StatusCodes.Status409Conflict, title: "User update rejected", detail: GetConflictMessage(result.ConflictCode), extensions: new Dictionary<string, object?> { ["code"] = result.ConflictCode }),
            _ => throw new InvalidOperationException("Unknown admin user mutation result.")
        };
    }

    private async Task<bool> IsCsrfRequestValidAsync()
    {
        try
        {
            await antiforgery.ValidateRequestAsync(HttpContext);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }

    private ObjectResult InvalidCsrf() => Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid CSRF token", detail: "The request could not be verified.");
    private ObjectResult UnauthorizedProblem() => Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthenticated", detail: "Please sign in again.");

    private static string GetConflictMessage(string? code) => code switch
    {
        "self_disable" => "You cannot disable your own account.",
        "self_role_change" => "You cannot change your own role.",
        "last_active_admin" => "The final active administrator cannot be disabled or demoted.",
        _ => "The requested user update conflicts with the current account state."
    };
}
