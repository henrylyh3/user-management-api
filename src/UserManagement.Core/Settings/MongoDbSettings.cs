using System.ComponentModel.DataAnnotations;

namespace UserManagement.Core.Settings;

public sealed class MongoDbSettings
{
    [Required]
    [RegularExpression(@"^mongodb(?:\+srv)?://.+", ErrorMessage = "MongoDB connection string must use mongodb:// or mongodb+srv://.")]
    public string ConnectionString { get; set; } = string.Empty;

    [Required, StringLength(64)]
    public string DatabaseName { get; set; } = string.Empty;
}
