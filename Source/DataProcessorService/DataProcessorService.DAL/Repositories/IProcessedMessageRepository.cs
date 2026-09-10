using Shared.Entities;

namespace DataProcessorService.DAL.Repositories;

public interface IProcessedMessageRepository
{
    Task<bool> ExistsAsync(Guid messageId, CancellationToken cancellationToken);

    Task AddAsync(DbProcessedMessage processedMessage, CancellationToken cancellationToken);
}
