using Azure.Storage.Queues;
using RegulatoryIntake.Application.Abstractions.Storage;
using RegulatoryIntake.Infrastructure.Configuration;

namespace RegulatoryIntake.Infrastructure.Services;

public sealed class AzureQueueStorageService : IQueueStorageService
{
    private readonly QueueClient _queueClient;

    public AzureQueueStorageService(
        QueueServiceClient queueServiceClient,
        Microsoft.Extensions.Options.IOptions<StorageOptions> storageOptions)
    {
        _queueClient = queueServiceClient.GetQueueClient(storageOptions.Value.Queues.DocumentProcessing);
    }

    public async Task EnqueueAsync(BinaryData message, CancellationToken cancellationToken = default)
    {
        await _queueClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await _queueClient.SendMessageAsync(message.ToString(), cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
