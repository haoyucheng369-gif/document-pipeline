using System;
using System.Collections.Generic;
using System.Text;

namespace CloudDocumentPipeline.Domain.Jobs
{
    public enum JobStatus
    {
        Pending = 0,
        Processing = 1,
        Succeeded = 2,
        Failed = 3
    }
}
