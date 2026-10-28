using System.Reflection;
using UserManagement.Web.Controllers;
using UserManagement.Web.Validation;

namespace UserManagement.UnitTests.Validation;

public class MongoObjectIdAttributeTests
{
    [Theory]
    [InlineData("507f1f77bcf86cd799439011", true)]
    [InlineData("not-an-object-id", false)]
    [InlineData("507f1f77bcf86cd79943901", false)]
    public void IsValid_ValidatesMongoObjectId(string value, bool expected)
    {
        var attribute = new MongoObjectIdAttribute();

        Assert.Equal(expected, attribute.IsValid(value));
    }

    [Fact]
    public void UserEndpoints_WithIdParameter_RequireMongoObjectIdValidation()
    {
        var endpointNames = new[]
        {
            nameof(UserController.GetUser),
            nameof(UserController.UpdateProfile),
            nameof(UserController.UpdateImageUrl),
            nameof(UserController.RemoveImageUrlAsync)
        };

        foreach (var endpointName in endpointNames)
        {
            var method = typeof(UserController).GetMethod(endpointName) ?? throw new InvalidOperationException($"Endpoint {endpointName} was not found.");
            var idParameter = method.GetParameters().Single(parameter => parameter.Name == "id");

            Assert.NotNull(idParameter.GetCustomAttribute<MongoObjectIdAttribute>());
        }
    }
}
