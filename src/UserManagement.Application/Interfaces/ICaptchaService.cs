namespace UserManagement.Application.Interfaces;

public interface ICaptchaService
{
    Task<bool> ValidateAsync(string? token, string expectedAction, string? remoteIpAddress, CancellationToken cancellationToken = default);
}
