using CloudDocumentPipeline.Application.Abstractions.Persistence;
using CloudDocumentPipeline.Domain.Jobs;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace CloudDocumentPipeline.Infrastructure.Persistence.Repositories
{
    public class JobRepository : IJobRepository
    {
        private readonly AppDbContext _dbContext;
        public JobRepository(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task AddAsync(Job job, CancellationToken cancellationToken = default)
        {
            await _dbContext.Jobs.AddAsync(job, cancellationToken);
        }

        public async Task<Job?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _dbContext.Jobs.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        }

        public async Task<List<Job>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return await _dbContext.Jobs.OrderByDescending(x=>x.CreatedAtUtc).ToListAsync(cancellationToken);
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
