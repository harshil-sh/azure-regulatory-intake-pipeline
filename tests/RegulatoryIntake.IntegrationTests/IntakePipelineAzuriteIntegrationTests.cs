using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RegulatoryIntake.Application.Services;
using RegulatoryIntake.Domain.Events;
using RegulatoryIntake.Domain.Messages;
using RegulatoryIntake.Domain.Metadata;
using RegulatoryIntake.Infrastructure.Services;

namespace RegulatoryIntake.IntegrationTests;

public sealed class IntakePipelineAzuriteIntegrationTests : IClassFixture<AzuriteFixture>
{
    private readonly AzuriteFixture _fixture;

    public IntakePipelineAzuriteIntegrationTests(AzuriteFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task HandleBlobCreatedEventsAsync_RoutesValidDocument_EndToEndAgainstAzurite()
    {
        await _fixture.ResetAsync().ConfigureAwait(false);

        var blobName = $"valid-regulatory-report-{Guid.NewGuid():N}.json";
        var rawContainer = _fixture.GetContainerClient(_fixture.StorageOptions.Containers.RawDocuments);
        var blobClient = rawContainer.GetBlobClient(blobName);

        await blobClient.UploadAsync(
            BinaryData.FromString("""{"document":"valid"}""").ToStream(),
            new BlobUploadOptions
            {
                Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [DocumentMetadataFieldNames.CorrelationId] = "corr-valid-001",
                    [DocumentMetadataFieldNames.DocumentType] = "sanctions",
                    [DocumentMetadataFieldNames.SourceSystem] = "local-portal"
                }
            }).ConfigureAwait(false);

        var orchestrator = CreateOrchestrator();
        var @event = CreateBlobCreatedEvent(blobClient.Uri, blobName, "valid-001");

        await orchestrator.HandleBlobCreatedEventsAsync([@event]).ConfigureAwait(false);

        Assert.False((await blobClient.ExistsAsync().ConfigureAwait(false)).Value);

        var validatedBlob = _fixture
            .GetContainerClient(_fixture.StorageOptions.Containers.ValidatedDocuments)
            .GetBlobClient(blobName);
        Assert.True((await validatedBlob.ExistsAsync().ConfigureAwait(false)).Value);

        var intakeEntity = await GetSingleIntakeEntityAsync().ConfigureAwait(false);
        Assert.Equal("Validated", intakeEntity.GetString("Status"));
        Assert.Equal(_fixture.StorageOptions.Containers.ValidatedDocuments, intakeEntity.GetString("ContainerName"));
        Assert.Equal("corr-valid-001", intakeEntity.GetString("CorrelationId"));
        Assert.True(string.IsNullOrWhiteSpace(intakeEntity.GetString("ValidationErrors")));

        var queueMessage = await GetSingleQueueMessageAsync().ConfigureAwait(false);
        Assert.Equal(blobName, queueMessage.BlobName);
        Assert.Equal(_fixture.StorageOptions.Containers.ValidatedDocuments, queueMessage.ContainerName);
        Assert.Equal("corr-valid-001", queueMessage.CorrelationId);
        Assert.Equal(validatedBlob.Uri, queueMessage.BlobUri);
    }

    [Fact]
    public async Task HandleBlobCreatedEventsAsync_RoutesInvalidDocument_ToQuarantineAgainstAzurite()
    {
        await _fixture.ResetAsync().ConfigureAwait(false);

        var blobName = $"invalid-regulatory-report-{Guid.NewGuid():N}.json";
        var rawContainer = _fixture.GetContainerClient(_fixture.StorageOptions.Containers.RawDocuments);
        var blobClient = rawContainer.GetBlobClient(blobName);

        await blobClient.UploadAsync(
            BinaryData.FromString("""{"document":"invalid"}""").ToStream(),
            new BlobUploadOptions
            {
                Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [DocumentMetadataFieldNames.DocumentType] = "sanctions"
                }
            }).ConfigureAwait(false);

        var orchestrator = CreateOrchestrator();
        var @event = CreateBlobCreatedEvent(blobClient.Uri, blobName, "invalid-001");

        await orchestrator.HandleBlobCreatedEventsAsync([@event]).ConfigureAwait(false);

        Assert.False((await blobClient.ExistsAsync().ConfigureAwait(false)).Value);

        var quarantineBlob = _fixture
            .GetContainerClient(_fixture.StorageOptions.Containers.QuarantineDocuments)
            .GetBlobClient(blobName);
        Assert.True((await quarantineBlob.ExistsAsync().ConfigureAwait(false)).Value);

        var intakeEntity = await GetSingleIntakeEntityAsync().ConfigureAwait(false);
        Assert.Equal("Quarantined", intakeEntity.GetString("Status"));
        Assert.Equal(_fixture.StorageOptions.Containers.QuarantineDocuments, intakeEntity.GetString("ContainerName"));
        Assert.Equal("client-request-invalid-001", intakeEntity.GetString("CorrelationId"));

        var validationErrors = intakeEntity.GetString("ValidationErrors");
        Assert.NotNull(validationErrors);
        Assert.Contains("correlationId", validationErrors, StringComparison.Ordinal);
        Assert.Contains("sourceSystem", validationErrors, StringComparison.Ordinal);

        var queueMessages = await _fixture.GetQueueClient().PeekMessagesAsync(maxMessages: 1).ConfigureAwait(false);
        Assert.Empty(queueMessages.Value);
    }

    private IntakeOrchestrator CreateOrchestrator()
    {
        var options = Options.Create(_fixture.StorageOptions);

        return new IntakeOrchestrator(
            new AzureBlobStorageService(_fixture.BlobServiceClient, options),
            new MetadataValidationService(),
            new Sha256ChecksumService(),
            new AzureTableStorageService(_fixture.TableServiceClient, options),
            new AzureQueueStorageService(_fixture.QueueServiceClient, options),
            NullLogger<IntakeOrchestrator>.Instance);
    }

    private async Task<TableEntity> GetSingleIntakeEntityAsync()
    {
        var entities = new List<TableEntity>();

        await foreach (var entity in _fixture
                           .GetIntakeTableClient()
                           .QueryAsync<TableEntity>(entity => entity.PartitionKey == "DocumentIntake"))
        {
            entities.Add(entity);
        }

        return Assert.Single(entities);
    }

    private async Task<DocumentProcessingQueueMessage> GetSingleQueueMessageAsync()
    {
        var response = await _fixture.GetQueueClient().PeekMessagesAsync(maxMessages: 1).ConfigureAwait(false);
        var queueMessage = Assert.Single(response.Value);
        var deserialized = BinaryData.FromString(queueMessage.MessageText)
            .ToObjectFromJson<DocumentProcessingQueueMessage>();

        Assert.NotNull(deserialized);
        return deserialized;
    }

    private static EventGridEventEnvelope<BlobCreatedEventData> CreateBlobCreatedEvent(
        Uri blobUri,
        string blobName,
        string eventId)
    {
        var containerName = blobUri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[1];

        return new EventGridEventEnvelope<BlobCreatedEventData>
        {
            Id = eventId,
            Topic = "/subscriptions/local/resourceGroups/dev/providers/Microsoft.Storage/storageAccounts/devstoreaccount1",
            Subject = $"/blobServices/default/containers/{containerName}/blobs/{blobName}",
            EventType = "Microsoft.Storage.BlobCreated",
            EventTime = new DateTimeOffset(2026, 03, 29, 10, 15, 30, TimeSpan.Zero),
            Data = new BlobCreatedEventData
            {
                Api = "PutBlob",
                ClientRequestId = $"client-request-{eventId}",
                RequestId = $"request-{eventId}",
                ETag = new ETag($"\"{eventId}\"").ToString(),
                ContentType = "application/json",
                ContentLength = 20,
                Url = blobUri,
                BlobType = "BlockBlob",
                Sequencer = "000000000000000000000000000000010000000000000001"
            },
            DataVersion = "1",
            MetadataVersion = "1"
        };
    }
}
