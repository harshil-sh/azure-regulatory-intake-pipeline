using Azure.Data.Tables;
using Microsoft.Extensions.Logging.Abstractions;
using RegulatoryIntake.Application.Abstractions.Storage;
using RegulatoryIntake.Application.Services;
using RegulatoryIntake.Domain.Events;
using RegulatoryIntake.Domain.Messages;
using RegulatoryIntake.Domain.Metadata;

namespace RegulatoryIntake.Application.Tests;

public sealed class IntakeOrchestratorTests
{
    [Fact]
    public async Task HandleBlobCreatedEventsAsync_RoutesValidDocument_PersistsRecord_AndPublishesQueueMessage()
    {
        var blobStorageService = new FakeBlobStorageService(
            BinaryData.FromString("valid file content"),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [DocumentMetadataFieldNames.CorrelationId] = "corr-123",
                [DocumentMetadataFieldNames.DocumentType] = "policy",
                [DocumentMetadataFieldNames.SourceSystem] = "portal"
            });
        var tableStorageService = new FakeTableStorageService();
        var queueStorageService = new FakeQueueStorageService();
        var orchestrator = new IntakeOrchestrator(
            blobStorageService,
            new MetadataValidationService(),
            new Sha256ChecksumService(),
            tableStorageService,
            queueStorageService,
            NullLogger<IntakeOrchestrator>.Instance);
        var @event = CreateBlobCreatedEvent("regulatory-report.pdf");

        await orchestrator.HandleBlobCreatedEventsAsync([@event]);

        Assert.Single(blobStorageService.CopyOperations);
        Assert.Equal(
            (BlobContainerName.RawDocuments, "regulatory-report.pdf", BlobContainerName.ValidatedDocuments, "regulatory-report.pdf"),
            blobStorageService.CopyOperations[0]);
        Assert.Single(blobStorageService.DeleteOperations);
        Assert.Equal((BlobContainerName.RawDocuments, "regulatory-report.pdf"), blobStorageService.DeleteOperations[0]);

        var persistedEntity = Assert.IsAssignableFrom<ITableEntity>(Assert.Single(tableStorageService.UpsertedEntities));
        Assert.Equal(TableName.DocumentIntake, Assert.Single(tableStorageService.TableNames));
        Assert.Equal("DocumentIntake", persistedEntity.PartitionKey);
        Assert.False(string.IsNullOrWhiteSpace(persistedEntity.RowKey));
        Assert.Equal("Validated", GetEntityProperty<string>(persistedEntity, "Status"));
        Assert.Equal("validated-documents", GetEntityProperty<string>(persistedEntity, "ContainerName"));
        Assert.Equal("corr-123", GetEntityProperty<string>(persistedEntity, "CorrelationId"));
        Assert.Null(GetEntityPropertyValue(persistedEntity, "ValidationErrors"));

        var queuePayload = Assert.Single(queueStorageService.Messages);
        var queueMessage = queuePayload.ToObjectFromJson<DocumentProcessingQueueMessage>();
        Assert.NotNull(queueMessage);
        Assert.Equal("validated-documents", queueMessage!.ContainerName);
        Assert.Equal("regulatory-report.pdf", queueMessage.BlobName);
        Assert.Equal("corr-123", queueMessage.CorrelationId);
        Assert.Equal(
            new Uri("http://127.0.0.1:10000/devstoreaccount1/validated-documents/regulatory-report.pdf"),
            queueMessage.BlobUri);
    }

    [Fact]
    public async Task DocumentProcessingService_ProcessAsync_PersistsCompletedProcessingRecord()
    {
        var tableStorageService = new FakeTableStorageService();
        var service = new DocumentProcessingService(
            tableStorageService,
            NullLogger<DocumentProcessingService>.Instance);
        var message = new DocumentProcessingQueueMessage
        {
            IntakeId = "intake-001",
            CorrelationId = "corr-123",
            BlobName = "regulatory-report.pdf",
            ContainerName = "validated-documents",
            BlobUri = new Uri("http://127.0.0.1:10000/devstoreaccount1/validated-documents/regulatory-report.pdf"),
            ContentType = "application/pdf",
            Checksum = "abc123",
            EnqueuedAtUtc = new DateTimeOffset(2026, 03, 29, 10, 15, 30, TimeSpan.Zero)
        };

        await service.ProcessAsync(message);

        var persistedEntity = Assert.IsAssignableFrom<ITableEntity>(Assert.Single(tableStorageService.UpsertedEntities));
        Assert.Equal(TableName.DocumentProcessing, Assert.Single(tableStorageService.TableNames));
        Assert.Equal("DocumentProcessing", persistedEntity.PartitionKey);
        Assert.Equal("intake-001", persistedEntity.RowKey);
        Assert.Equal("Completed", GetEntityProperty<string>(persistedEntity, "Status"));
        Assert.Equal("corr-123", GetEntityProperty<string>(persistedEntity, "CorrelationId"));
        Assert.Equal("validated-documents", GetEntityProperty<string>(persistedEntity, "ContainerName"));
        Assert.Equal(
            "http://127.0.0.1:10000/devstoreaccount1/validated-documents/regulatory-report.pdf",
            GetEntityProperty<string>(persistedEntity, "BlobUri"));
        Assert.NotNull(GetEntityProperty<DateTimeOffset>(persistedEntity, "ProcessedAtUtc"));
    }

    [Fact]
    public async Task HandleBlobCreatedEventsAsync_RoutesInvalidDocumentToQuarantine_AndDoesNotPublishQueueMessage()
    {
        var blobStorageService = new FakeBlobStorageService(
            BinaryData.FromString("invalid file content"),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [DocumentMetadataFieldNames.DocumentType] = "policy"
            });
        var tableStorageService = new FakeTableStorageService();
        var queueStorageService = new FakeQueueStorageService();
        var orchestrator = new IntakeOrchestrator(
            blobStorageService,
            new MetadataValidationService(),
            new Sha256ChecksumService(),
            tableStorageService,
            queueStorageService,
            NullLogger<IntakeOrchestrator>.Instance);
        var @event = CreateBlobCreatedEvent("missing-metadata.pdf");

        await orchestrator.HandleBlobCreatedEventsAsync([@event]);

        Assert.Single(blobStorageService.CopyOperations);
        Assert.Equal(
            (BlobContainerName.RawDocuments, "missing-metadata.pdf", BlobContainerName.QuarantineDocuments, "missing-metadata.pdf"),
            blobStorageService.CopyOperations[0]);
        Assert.Single(blobStorageService.DeleteOperations);

        var persistedEntity = Assert.IsAssignableFrom<ITableEntity>(Assert.Single(tableStorageService.UpsertedEntities));
        Assert.Equal("Quarantined", GetEntityProperty<string>(persistedEntity, "Status"));
        Assert.Equal("quarantine-documents", GetEntityProperty<string>(persistedEntity, "ContainerName"));
        Assert.Equal("client-123", GetEntityProperty<string>(persistedEntity, "CorrelationId"));
        Assert.Contains("correlationId", GetEntityProperty<string>(persistedEntity, "ValidationErrors"));
        Assert.Contains("sourceSystem", GetEntityProperty<string>(persistedEntity, "ValidationErrors"));
        Assert.Empty(queueStorageService.Messages);
    }

    private static EventGridEventEnvelope<BlobCreatedEventData> CreateBlobCreatedEvent(string blobName) =>
        new()
        {
            Id = "evt-001",
            Topic = "/subscriptions/local/resourceGroups/dev/providers/Microsoft.Storage/storageAccounts/devstoreaccount1",
            Subject = $"/blobServices/default/containers/raw-documents/blobs/{blobName}",
            EventType = "Microsoft.Storage.BlobCreated",
            EventTime = new DateTimeOffset(2026, 03, 29, 10, 15, 30, TimeSpan.Zero),
            Data = new BlobCreatedEventData
            {
                Api = "PutBlob",
                ClientRequestId = "client-123",
                RequestId = "request-456",
                ETag = "0x8DB3A3C4D5E6F70",
                ContentType = "application/pdf",
                ContentLength = 2048,
                Url = new Uri($"http://127.0.0.1:10000/devstoreaccount1/raw-documents/{blobName}"),
                BlobType = "BlockBlob",
                Sequencer = "000000000000000000000000000000010000000000000001"
            },
            DataVersion = "1",
            MetadataVersion = "1"
        };

    private static T GetEntityProperty<T>(ITableEntity entity, string propertyName)
    {
        var value = GetEntityPropertyValue(entity, propertyName);
        return Assert.IsType<T>(value);
    }

    private static object? GetEntityPropertyValue(ITableEntity entity, string propertyName)
    {
        var property = entity.GetType().GetProperty(propertyName);
        Assert.NotNull(property);
        return property!.GetValue(entity);
    }

    private sealed class FakeBlobStorageService : IBlobStorageService
    {
        private readonly BinaryData _content;
        private readonly IReadOnlyDictionary<string, string> _metadata;
        private readonly Dictionary<BlobContainerName, string> _containerNames = new()
        {
            [BlobContainerName.RawDocuments] = "raw-documents",
            [BlobContainerName.ValidatedDocuments] = "validated-documents",
            [BlobContainerName.QuarantineDocuments] = "quarantine-documents"
        };

        public FakeBlobStorageService(BinaryData content, IReadOnlyDictionary<string, string> metadata)
        {
            _content = content;
            _metadata = metadata;
        }

        public List<(BlobContainerName SourceContainer, string SourceBlobName, BlobContainerName DestinationContainer, string DestinationBlobName)> CopyOperations { get; } = [];

        public List<(BlobContainerName Container, string BlobName)> DeleteOperations { get; } = [];

        public Task CopyAsync(
            BlobContainerName sourceContainerName,
            string sourceBlobName,
            BlobContainerName destinationContainerName,
            string destinationBlobName,
            CancellationToken cancellationToken = default)
        {
            CopyOperations.Add((sourceContainerName, sourceBlobName, destinationContainerName, destinationBlobName));
            return Task.CompletedTask;
        }

        public Task DeleteIfExistsAsync(
            BlobContainerName containerName,
            string blobName,
            CancellationToken cancellationToken = default)
        {
            DeleteOperations.Add((containerName, blobName));
            return Task.CompletedTask;
        }

        public Task<BinaryData> DownloadAsync(
            BlobContainerName containerName,
            string blobName,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_content);

        public Task<IReadOnlyDictionary<string, string>> GetMetadataAsync(
            BlobContainerName containerName,
            string blobName,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_metadata);

        public Uri GetBlobUri(BlobContainerName containerName, string blobName) =>
            new($"http://127.0.0.1:10000/devstoreaccount1/{_containerNames[containerName]}/{blobName}");
    }

    private sealed class FakeQueueStorageService : IQueueStorageService
    {
        public List<BinaryData> Messages { get; } = [];

        public Task EnqueueAsync(BinaryData message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTableStorageService : ITableStorageService
    {
        public List<TableName> TableNames { get; } = [];

        public List<object> UpsertedEntities { get; } = [];

        public Task UpsertEntityAsync<T>(
            TableName tableName,
            T entity,
            TableUpdateMode updateMode = TableUpdateMode.Replace,
            CancellationToken cancellationToken = default)
            where T : class, ITableEntity
        {
            TableNames.Add(tableName);
            UpsertedEntities.Add(entity);
            return Task.CompletedTask;
        }
    }
}
