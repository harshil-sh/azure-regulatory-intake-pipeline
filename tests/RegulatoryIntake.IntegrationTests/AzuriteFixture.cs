using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using RegulatoryIntake.Infrastructure.Configuration;

namespace RegulatoryIntake.IntegrationTests;

public sealed class AzuriteFixture : IAsyncLifetime
{
    private const string DevelopmentStorageConnectionString = "UseDevelopmentStorage=true";

    private readonly string _resourceSuffix = Guid.NewGuid().ToString("N")[..12];

    public BlobServiceClient BlobServiceClient { get; } = new(DevelopmentStorageConnectionString);

    public QueueServiceClient QueueServiceClient { get; } = new(DevelopmentStorageConnectionString);

    public TableServiceClient TableServiceClient { get; } = new(DevelopmentStorageConnectionString);

    public StorageOptions StorageOptions { get; }

    public AzuriteFixture()
    {
        StorageOptions = new StorageOptions
        {
            ConnectionString = DevelopmentStorageConnectionString,
            Containers = new StorageOptions.BlobContainerOptions
            {
                RawDocuments = $"raw-documents-it-{_resourceSuffix}",
                ValidatedDocuments = $"validated-documents-it-{_resourceSuffix}",
                QuarantineDocuments = $"quarantine-documents-it-{_resourceSuffix}"
            },
            Queues = new StorageOptions.QueueOptions
            {
                DocumentProcessing = $"document-processing-it-{_resourceSuffix}"
            },
            Tables = new StorageOptions.TableOptions
            {
                DocumentIntake = $"DocumentIntakeIt{_resourceSuffix}",
                DocumentProcessing = $"DocumentProcessingIt{_resourceSuffix}"
            }
        };
    }

    public async Task InitializeAsync()
    {
        try
        {
            await ResetAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(
                "Azurite must be running on the default local ports before executing integration tests. See tests/RegulatoryIntake.IntegrationTests/README.md.",
                exception);
        }
    }

    public async Task DisposeAsync()
    {
        await DeleteIfExistsAsync(StorageOptions.Containers.RawDocuments).ConfigureAwait(false);
        await DeleteIfExistsAsync(StorageOptions.Containers.ValidatedDocuments).ConfigureAwait(false);
        await DeleteIfExistsAsync(StorageOptions.Containers.QuarantineDocuments).ConfigureAwait(false);
        await QueueServiceClient
            .GetQueueClient(StorageOptions.Queues.DocumentProcessing)
            .DeleteIfExistsAsync()
            .ConfigureAwait(false);
        await DeleteTableIfExistsAsync(StorageOptions.Tables.DocumentIntake).ConfigureAwait(false);
        await DeleteTableIfExistsAsync(StorageOptions.Tables.DocumentProcessing).ConfigureAwait(false);
    }

    public async Task ResetAsync()
    {
        await RecreateContainerAsync(StorageOptions.Containers.RawDocuments).ConfigureAwait(false);
        await RecreateContainerAsync(StorageOptions.Containers.ValidatedDocuments).ConfigureAwait(false);
        await RecreateContainerAsync(StorageOptions.Containers.QuarantineDocuments).ConfigureAwait(false);

        var queueClient = QueueServiceClient.GetQueueClient(StorageOptions.Queues.DocumentProcessing);
        await queueClient.DeleteIfExistsAsync().ConfigureAwait(false);
        await queueClient.CreateIfNotExistsAsync().ConfigureAwait(false);

        await RecreateTableAsync(StorageOptions.Tables.DocumentIntake).ConfigureAwait(false);
        await RecreateTableAsync(StorageOptions.Tables.DocumentProcessing).ConfigureAwait(false);
    }

    public BlobContainerClient GetContainerClient(string containerName) =>
        BlobServiceClient.GetBlobContainerClient(containerName);

    public QueueClient GetQueueClient() =>
        QueueServiceClient.GetQueueClient(StorageOptions.Queues.DocumentProcessing);

    public TableClient GetIntakeTableClient() =>
        TableServiceClient.GetTableClient(StorageOptions.Tables.DocumentIntake);

    private async Task RecreateContainerAsync(string containerName)
    {
        var containerClient = BlobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.DeleteIfExistsAsync().ConfigureAwait(false);
        await containerClient.CreateIfNotExistsAsync().ConfigureAwait(false);
    }

    private async Task DeleteIfExistsAsync(string containerName)
    {
        var containerClient = BlobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.DeleteIfExistsAsync().ConfigureAwait(false);
    }

    private async Task RecreateTableAsync(string tableName)
    {
        await DeleteTableIfExistsAsync(tableName).ConfigureAwait(false);
        await TableServiceClient.GetTableClient(tableName).CreateIfNotExistsAsync().ConfigureAwait(false);
    }

    private async Task DeleteTableIfExistsAsync(string tableName)
    {
        try
        {
            await TableServiceClient.DeleteTableAsync(tableName).ConfigureAwait(false);
        }
        catch (RequestFailedException exception) when (exception.Status == 404)
        {
        }
    }
}
