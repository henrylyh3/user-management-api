using UserManagement.Core.DTOs;

namespace UserManagement.Application.Interfaces;

public interface IAccountService
{
    Task<bool> SignupAsync(SignupRequest request, CancellationToken cancellationToken = default);
    Task RequestEmailVerificationAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> VerifyEmailAsync(string token, CancellationToken cancellationToken = default);
    Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> ResetPasswordAsync(string token, string newPassword, CancellationToken cancellationToken = default);
    Task<bool> ChangePasswordAsync(string userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);
    Task<PersonalDataExportDto?> ExportPersonalDataAsync(string userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAccountAsync(string userId, string currentPassword, CancellationToken cancellationToken = default);
}
