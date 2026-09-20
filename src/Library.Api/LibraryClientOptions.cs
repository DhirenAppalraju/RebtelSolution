using System.ComponentModel.DataAnnotations;

namespace Library.Api
{
    public sealed class LibraryClientOptions
    {
        public const string SectionName = "Library";

        /// <summary>Where the lending service lives. Echoed in the 503 detail so a reviewer sees what to start.</summary>
        [Required]
        public string GrpcEndpoint { get; set; } = "http://localhost:5210";

        [Range(1, 300)]
        public int GrpcDeadlineSeconds { get; set; } = 10;
    }
}
