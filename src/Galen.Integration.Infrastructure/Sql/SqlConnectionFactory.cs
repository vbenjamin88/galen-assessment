using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace Galen.Integration.Infrastructure.Sql;

/// <summary>
/// Creates SqlConnection with retry/resiliency via Polly at the call site.
/// </summary>
public sealed class SqlConnectionFactory
{
    private readonly RecordRepositoryOptions _options;

    public SqlConnectionFactory(IOptions<RecordRepositoryOptions> options)
    {
        _options = options.Value;
    }

    public SqlConnection Create()
    {
        return new SqlConnection(_options.ConnectionString);
    }
}
