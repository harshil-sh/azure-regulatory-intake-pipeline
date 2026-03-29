using RegulatoryIntake.Domain.Messages;

namespace RegulatoryIntake.Application.Abstractions;

public interface IDocumentProcessingService
{
    Task ProcessAsync(
        DocumentProcessingQueueMessage message,
        CancellationToken cancellationToken = default);
}
