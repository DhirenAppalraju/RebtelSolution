using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Library.IntegrationTests
{
    /// <summary>
    /// Runs a competing write once, just before the context under test saves: the only
    /// interleaving that reaches a unique index rather than the `if` in front of it.
    /// <para>
    /// Deterministic: the ordering is forced, not waited for.
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

        /// <summary>True once the competing write has run.</summary>
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
