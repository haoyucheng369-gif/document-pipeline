using CloudDocumentPipeline.Domain.Jobs;
using System;
using System.Collections.Generic;
using System.Text;

namespace CloudDocumentPipeline.Application.Abstractions.Persistence
{
    public interface IJobRepository
    {
        Task AddAsync(Job job, CancellationToken cancellationToken = default);
        Task<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<List<Job>> GetAllAsync(CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
