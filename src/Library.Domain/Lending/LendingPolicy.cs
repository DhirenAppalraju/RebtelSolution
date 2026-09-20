using System.ComponentModel.DataAnnotations;

namespace Library.Domain.Lending
{
    /// <summary>Librarian policy, bound from configuration and validated on start-up.</summary>
    public sealed class LendingPolicy
    {
        public const string SectionName = "Lending";

        [Range(1, 365)]
        public int LoanPeriodDays { get; set; } = 21;

        [Range(1, 100)]
        public int MaxConcurrentLoans { get; set; } = 5;

        [Range(1, 1000)]
        public int DefaultReportLimit { get; set; } = 10;

        [Range(1, 1000)]
        public int MaxReportLimit { get; set; } = 100;
    }
}
