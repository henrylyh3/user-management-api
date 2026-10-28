using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;

namespace UserManagement.Web.Validation;

[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
public sealed class MongoObjectIdAttribute : ValidationAttribute
{
    public MongoObjectIdAttribute()
    {
        ErrorMessage = "The {0} field must be a valid MongoDB ObjectId.";
    }

    public override bool IsValid(object? value)
    {
        return value is string id && ObjectId.TryParse(id, out _);
    }
}
