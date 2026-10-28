using System.ComponentModel.DataAnnotations;
using UserManagement.Core.DTOs;

namespace UserManagement.UnitTests.Validation;

public sealed class AuditEventQueryTests
{
    [Fact]
    public void Validate_DefaultQuery_IsValid()
    {
        Assert.Empty(Validate(new AuditEventQuery()));
    }

    [Fact]
    public void Validate_InvalidActorAndPageSize_IsInvalid()
    {
        var errors = Validate(new AuditEventQuery { ActorUserId = "not-an-object-id", PageSize = 101 });

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(AuditEventQuery.ActorUserId)));
        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(AuditEventQuery.PageSize)));
    }

    [Fact]
    public void Validate_ReversedDateRange_IsInvalid()
    {
        var errors = Validate(new AuditEventQuery { From = new DateTime(2026, 10, 22), To = new DateTime(2026, 10, 21) });

        Assert.Contains(errors, error => error.MemberNames.Contains(nameof(AuditEventQuery.From)) && error.MemberNames.Contains(nameof(AuditEventQuery.To)));
    }

    private static List<ValidationResult> Validate(AuditEventQuery query)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(query, new ValidationContext(query), results, validateAllProperties: true);
        return results;
    }
}
