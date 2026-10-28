namespace UserManagement.Core.DTOs;

public sealed class PersonalDataExportDto
{
    public DateTime ExportedOn { get; init; }
    public string Id { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime? DisabledOn { get; init; }
    public string? ImageUrl { get; init; }
    public bool IsEmailVerified { get; init; }
    public DateTime CreatedOn { get; init; }
    public DateTime ModifiedOn { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public string ModifiedBy { get; init; } = string.Empty;
    public IReadOnlyCollection<UserSessionDto> ActiveSessions { get; init; } = [];
}
