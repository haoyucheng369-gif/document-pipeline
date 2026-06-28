using System;
using System.Collections.Generic;
using System.Text;

namespace CloudDocumentPipeline.Application.Jobs
{
    public sealed class JobDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string Type { get; set; } = default!;
        public string Status { get; set; } = default!;
        public int RetryCount { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? StartedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
