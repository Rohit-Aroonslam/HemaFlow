using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Project.Services
{
    // Counts the SQL commands (and the time spent waiting for the database) of the current web request,
    // so the "Server-Timing" header and the slow-request log can say "12 queries, 940 ms in the database".
    public class DbStat { public int Queries; public double Ms; }

    public class DbTimingInterceptor : DbCommandInterceptor
    {
        public static readonly AsyncLocal<DbStat?> Current = new();

        private static void Add(TimeSpan d) { var s = Current.Value; if (s != null) { Interlocked.Increment(ref s.Queries); s.Ms += d.TotalMilliseconds; } }

        public override DbDataReader ReaderExecuted(DbCommand c, CommandExecutedEventData e, DbDataReader r) { Add(e.Duration); return r; }
        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand c, CommandExecutedEventData e, DbDataReader r, CancellationToken t = default) { Add(e.Duration); return new(r); }
        public override object? ScalarExecuted(DbCommand c, CommandExecutedEventData e, object? r) { Add(e.Duration); return r; }
        public override ValueTask<object?> ScalarExecutedAsync(DbCommand c, CommandExecutedEventData e, object? r, CancellationToken t = default) { Add(e.Duration); return new(r); }
        public override int NonQueryExecuted(DbCommand c, CommandExecutedEventData e, int r) { Add(e.Duration); return r; }
        public override ValueTask<int> NonQueryExecutedAsync(DbCommand c, CommandExecutedEventData e, int r, CancellationToken t = default) { Add(e.Duration); return new(r); }
    }
}
