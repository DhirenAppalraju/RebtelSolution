using System.ComponentModel.DataAnnotations;

namespace Library.Api
{
    public sealed class LibraryClientOptions
    {
        public const string SectionName = "Library";

        /// <summary>Lending service address; echoed in the 503 detail.</summary>
        [Required]
        public string GrpcEndpoint { get; set; } = "http://localhost:5210";

        [Range(1, 300)]
        public int GrpcDeadlineSeconds { get; set; } = 10;
    }
}
