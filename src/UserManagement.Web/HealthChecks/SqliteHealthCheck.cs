using Microsoft.Extensions.Diagnostics.HealthChecks;
using UserManagement.DataAccess.Database;

namespace UserManagement.Web.HealthChecks;

public sealed class SqliteHealthCheck(SqliteDatabase database) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var connection = database.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1";
        await command.ExecuteScalarAsync(cancellationToken);
        return HealthCheckResult.Healthy();
    }
}
