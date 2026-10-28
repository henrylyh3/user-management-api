namespace UserManagement.Core.Models;

public static class UserRoles
{
    public const string User = "User";
    public const string Admin = "Admin";

    public static bool IsSupported(string role) => role is User or Admin;
}
