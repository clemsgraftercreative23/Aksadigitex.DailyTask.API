using System.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Infrastructure;

/// <summary>
/// Execution strategy yang retry pada transient failures termasuk SocketException
/// (connection forcibly closed by remote host) saat koneksi ke PostgreSQL cloud.
/// </summary>
public class ResilientNpgsqlExecutionStrategy : ExecutionStrategy
{
    public ResilientNpgsqlExecutionStrategy(ExecutionStrategyDependencies dependencies)
        : base(dependencies, maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5))
    {
    }

    protected override bool ShouldRetryOn(Exception exception)
    {
        // NpgsqlException yang transient (dari Npgsql)
        if (exception is NpgsqlException npgsqlEx && npgsqlEx.IsTransient)
            return true;

        // Retry pada connection errors yang umum di cloud DB
        return IsConnectionError(exception);
    }

    private static bool IsConnectionError(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
        {
            if (e is SocketException se && IsTransientSocketError(se.SocketErrorCode))
                return true;
            if (e is IOException && (e.Message?.Contains("forcibly closed", StringComparison.OrdinalIgnoreCase) == true
                || e.Message?.Contains("connection", StringComparison.OrdinalIgnoreCase) == true))
                return true;
        }
        return false;
    }

    private static bool IsTransientSocketError(SocketError code)
    {
        return code is
            SocketError.ConnectionReset or
            SocketError.ConnectionAborted or
            SocketError.TimedOut or
            SocketError.HostNotFound or
            (SocketError)10054; // WSAECONNRESET - connection forcibly closed
    }
}
