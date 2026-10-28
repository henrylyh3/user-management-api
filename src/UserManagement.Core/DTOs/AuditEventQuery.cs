using System.ComponentModel.DataAnnotations;
using UserManagement.Core.Models;

namespace UserManagement.Core.DTOs;

public sealed class AuditEventQuery : IValidatableObject
{
    [Range(1, 1_000_000)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;

    [StringLength(64)]
    public string? Module { get; set; }

    [StringLength(64)]
    public string? Action { get; set; }

    public AuditOutcome? Outcome { get; set; }

    [RegularExpression("^[a-fA-F0-9]{24}$", ErrorMessage = "ActorUserId must be a valid MongoDB ObjectId.")]
    public string? ActorUserId { get; set; }

    [StringLength(64)]
    public string? ResourceType { get; set; }

    [StringLength(128)]
    public string? ResourceId { get; set; }

    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (From.HasValue && To.HasValue && From > To)
        {
            yield return new ValidationResult("From must be earlier than or equal to To.", [nameof(From), nameof(To)]);
        }
    }
}
