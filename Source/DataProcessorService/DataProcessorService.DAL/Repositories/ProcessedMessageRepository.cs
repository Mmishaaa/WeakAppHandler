using DataProcessorService.DAL.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataProcessorService.DAL.Repositories;

public sealed class ProcessedMessageRepository(ProcessorDbContext dbContext) : IProcessedMessageRepository
{
    public async Task<bool> ExistsAsync(Guid messageId, CancellationToken cancellationToken) =>
        await dbContext.ProcessedMessages.AnyAsync(
            message => message.MessageId == messageId,
            cancellationToken);

    public async Task AddAsync(DbProcessedMessage processedMessage, CancellationToken cancellationToken) =>
        await dbContext.ProcessedMessages.AddAsync(processedMessage, cancellationToken);
}
