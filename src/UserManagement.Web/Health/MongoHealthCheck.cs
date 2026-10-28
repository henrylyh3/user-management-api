using Microsoft.Extensions.Diagnostics.HealthChecks;
using UserManagement.DataAccess.Interfaces;

namespace UserManagement.Web.Health;

public sealed class MongoHealthCheck : IHealthCheck
{
    private readonly IUserRepository _userRepository;

    public MongoHealthCheck(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await _userRepository.PingAsync(cancellationToken);
            return HealthCheckResult.Healthy("MongoDB is reachable.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("MongoDB is unavailable.", exception);
        }
    }
}
