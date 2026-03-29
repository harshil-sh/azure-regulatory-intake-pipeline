namespace RegulatoryIntake.Application.Abstractions.Storage;

public interface IQueueStorageService
{
    Task EnqueueAsync(BinaryData message, CancellationToken cancellationToken = default);
}
