namespace UserManagement.Core.DTOs;

public sealed class ClientSessionInfo
{
    public string UserAgent { get; init; } = "Unknown device";
    public string IpAddress { get; init; } = "Unknown";
}
