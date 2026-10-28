using System.ComponentModel.DataAnnotations;
using UserManagement.Core.DTOs;

namespace UserManagement.UnitTests.Validation;

public class RequestValidationTests
{
    [Fact]
    public void SignupRequest_ValuesExceedMaximumLengths_IsInvalid()
    {
        var request = new SignupRequest
        {
            FirstName = new string('a', 101),
            LastName = new string('b', 101),
            Email = $"{new string('c', 243)}@example.com",
            Password = new string('d', 129)
        };

        var results = Validate(request);

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(SignupRequest.FirstName)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(SignupRequest.LastName)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(SignupRequest.Email)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(SignupRequest.Password)));
    }

    [Fact]
    public void SigninRequest_ValuesExceedMaximumLengths_IsInvalid()
    {
        var request = new SigninRequest
        {
            Email = $"{new string('a', 243)}@example.com",
            Password = new string('b', 129)
        };

        var results = Validate(request);

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(SigninRequest.Email)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(SigninRequest.Password)));
    }

    [Fact]
    public void UpdateUserProfileRequest_NamesExceedMaximumLengths_IsInvalid()
    {
        var request = new UpdateUserProfileRequest
        {
            FirstName = new string('a', 101),
            LastName = new string('b', 101)
        };

        var results = Validate(request);

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(UpdateUserProfileRequest.FirstName)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(UpdateUserProfileRequest.LastName)));
    }

    [Fact]
    public void ResetPasswordRequest_InvalidTokenAndPasswordLengths_IsInvalid()
    {
        var request = new ResetPasswordRequest { Token = new string('a', 257), NewPassword = "short" };

        var results = Validate(request);

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(ResetPasswordRequest.Token)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(ResetPasswordRequest.NewPassword)));
    }

    [Fact]
    public void PaginationRequest_OutOfRangeValues_IsInvalid()
    {
        var request = new PaginationRequest { Page = 0, PageSize = 101 };

        var results = Validate(request);

        Assert.Contains(results, result => result.MemberNames.Contains(nameof(PaginationRequest.Page)));
        Assert.Contains(results, result => result.MemberNames.Contains(nameof(PaginationRequest.PageSize)));
    }

    [Fact]
    public void AdminUserRequests_UnsupportedValues_AreInvalid()
    {
        Assert.Contains(Validate(new ChangeUserRoleRequest { Role = "Owner" }), result => result.MemberNames.Contains(nameof(ChangeUserRoleRequest.Role)));
        Assert.Contains(Validate(new ChangeUserStatusRequest { Status = "Suspended" }), result => result.MemberNames.Contains(nameof(ChangeUserStatusRequest.Status)));
    }

    private static List<ValidationResult> Validate(object value)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(value, new ValidationContext(value), results, validateAllProperties: true);
        return results;
    }
}
