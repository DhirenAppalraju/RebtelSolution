using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Library.IntegrationTests
{
    /// <summary>
    /// Runs a competing write exactly once, immediately before the context under test saves. That
    /// places the other writer *between* the read and the write of the call being tested, which is
    /// the only interleaving that reaches a unique index rather than the `if` in front of it.
    /// <para>
    /// Deterministic by construction: the ordering is forced, not waited for. A race tested by
    /// timing is a test that passes or fails on timing.
    /// </para>
    /// </summary>
    public sealed class StageTheRace : SaveChangesInterceptor
    {
        private readonly Func<Task> _competingWrite;
        private bool _fired;

        public StageTheRace(Func<Task> competingWrite)
        {
            _competingWrite = competingWrite;
        }

        /// <summary>True once the competing write has run, so a test can prove it was staged.</summary>
        public bool Fired
        {
            get { return _fired; }
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (!_fired)
            {
                _fired = true;
                await _competingWrite();
            }

            return result;
        }
    }
}
