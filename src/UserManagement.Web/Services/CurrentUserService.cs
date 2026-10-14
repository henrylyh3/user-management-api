using System.Security.Claims;
using UserManagement.Application.Interfaces;

namespace UserManagement.Web.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(
        IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User =>
        _httpContextAccessor.HttpContext?.User;

    public string? UserId => User?.FindFirstValue("sub");
    public string? Email => User?.FindFirstValue("email");
    public string? Role => User?.FindFirstValue("role");

    public bool IsAuthenticated =>
        User?.Identity?.IsAuthenticated == true;

    public bool IsAdmin =>
        User?.IsInRole("Admin") == true;
}