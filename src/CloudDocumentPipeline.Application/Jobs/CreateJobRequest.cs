using System;
using System.Collections.Generic;
using System.Text;

namespace CloudDocumentPipeline.Application.Jobs
{
    public sealed class CreateJobRequest
    {
        public string Name { get; set; } = default!;
        public string Type { get; set; } = default!;
        public string PayloadJson { get; set; } = "{}";
    }
}
