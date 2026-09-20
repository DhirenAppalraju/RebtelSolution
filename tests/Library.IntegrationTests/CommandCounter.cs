using System.Collections.Concurrent;
using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Library.IntegrationTests
{
    /// <summary>Counts round trips and keeps the SQL: catches a materialised cohort or an N+1.</summary>
    public sealed class CommandCounter : DbCommandInterceptor
    {
        private readonly ConcurrentQueue<string> _texts = new ConcurrentQueue<string>();

        public int Count
        {
            get { return _texts.Count; }
        }

        public IReadOnlyCollection<string> Texts
        {
            get { return _texts; }
        }

        public void Reset()
        {
            _texts.Clear();
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            _texts.Enqueue(command.CommandText);
            return base.ReaderExecuting(command, eventData, result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            _texts.Enqueue(command.CommandText);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
