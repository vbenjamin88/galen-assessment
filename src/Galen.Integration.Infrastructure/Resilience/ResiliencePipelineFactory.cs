using Microsoft.Data.SqlClient;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace Galen.Integration.Infrastructure.Resilience;

/// <summary>
/// Centralized Polly resilience pipelines for SQL and blob operations.
/// </summary>
public static class ResiliencePipelineFactory
{
    private static readonly HashSet<int> SqlTransientErrors = [4060, 40197, 40501, 40613, 49918, 49919, 49920, -2, 64];

    public static ResiliencePipeline CreateSqlPipeline()
    {
        var retry = new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromMilliseconds(500),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            ShouldHandle = new PredicateBuilder().Handle<SqlException>(ex => SqlTransientErrors.Contains(ex.Number))
        };

        var circuitBreaker = new CircuitBreakerStrategyOptions
        {
            FailureRatio = 0.5,
            MinimumThroughput = 5,
            BreakDuration = TimeSpan.FromSeconds(30),
            ShouldHandle = new PredicateBuilder().Handle<SqlException>(ex => SqlTransientErrors.Contains(ex.Number))
        };

        return new ResiliencePipelineBuilder()
            .AddRetry(retry)
            .AddCircuitBreaker(circuitBreaker)
            .Build();
    }

    public static ResiliencePipeline CreateBlobPipeline()
    {
        var retry = new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromSeconds(1),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            ShouldHandle = new PredicateBuilder()
                .Handle<HttpRequestException>()
                .Handle<TimeoutException>()
        };

        return new ResiliencePipelineBuilder()
            .AddRetry(retry)
            .Build();
    }
}
