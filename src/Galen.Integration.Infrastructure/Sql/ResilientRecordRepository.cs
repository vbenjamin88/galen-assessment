using Galen.Integration.Application.Services;
using Polly;
using Polly.Registry;
using Galen.Integration.Domain;
using Galen.Integration.Infrastructure.Resilience;
using Microsoft.Extensions.DependencyInjection;

namespace Galen.Integration.Infrastructure.Sql;

/// <summary>
/// Decorator that wraps RecordRepository with Polly resilience (retry + circuit breaker).
/// </summary>
public sealed class ResilientRecordRepository : IRecordRepository
{
    private readonly IRecordRepository _inner;
    private readonly ResiliencePipeline _pipeline;

    public ResilientRecordRepository(IRecordRepository inner)
    {
        _inner = inner;
        _pipeline = ResiliencePipelineFactory.CreateSqlPipeline();
    }

    public async Task<int> SaveBatchAsync(
        IReadOnlyList<CanonicalRecord> records,
        string sourceFileName,
        CancellationToken cancellationToken = default)
    {
        var context = ResilienceContextPool.Shared.Get(cancellationToken);
        try
        {
            return await _pipeline.ExecuteAsync(static async (ctx, state) =>
                await state.inner.SaveBatchAsync(state.records, state.sourceFileName, ctx.CancellationToken),
                context, (inner: _inner, records, sourceFileName));
        }
        finally
        {
            ResilienceContextPool.Shared.Return(context);
        }
    }
}
