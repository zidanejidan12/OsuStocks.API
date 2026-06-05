using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;
using System.Threading;

namespace OsuStocks.Api.IntegrationTests.Infrastructure;

internal sealed class QueryCountingCommandInterceptor : DbCommandInterceptor
{
    private static readonly string[] TargetTables =
    [
        "\"holdings\"",
        "\"portfolios\"",
        "\"trades\"",
        "\"player_stocks\"",
        "\"tracked_players\""
    ];

    private int _selectCommandCount;

    public int SelectCommandCount => Volatile.Read(ref _selectCommandCount);

    public void Reset()
    {
        Interlocked.Exchange(ref _selectCommandCount, 0);
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        Count(command);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Count(command);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        Count(command);
        return base.ScalarExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        Count(command);
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    private void Count(DbCommand command)
    {
        if (!command.CommandText.Contains("select", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!TargetTables.Any(table => command.CommandText.Contains(table, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        Interlocked.Increment(ref _selectCommandCount);
    }
}
