using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using RegulatoryIntake.Application.Abstractions;
using RegulatoryIntake.Application.Abstractions.Storage;
using RegulatoryIntake.Domain.Entities;
using RegulatoryIntake.Domain.Events;
using RegulatoryIntake.Domain.Messages;
using RegulatoryIntake.Domain.Metadata;
using System.Text.Json;

namespace RegulatoryIntake.Application.Services;

public sealed class IntakeOrchestrator : IIntakeOrchestrator
{
    private const string IntakeTablePartitionKey = "DocumentIntake";

    private readonly IBlobStorageService _blobStorageService;
    private readonly IMetadataValidationService _metadataValidationService;
    private readonly IChecksumService _checksumService;
    private readonly ITableStorageService _tableStorageService;
    private readonly IQueueStorageService _queueStorageService;
    private readonly ILogger<IntakeOrchestrator> _logger;

    public IntakeOrchestrator(
        IBlobStorageService blobStorageService,
        IMetadataValidationService metadataValidationService,
        IChecksumService checksumService,
        ITableStorageService tableStorageService,
        IQueueStorageService queueStorageService,
        ILogger<IntakeOrchestrator> logger)
    {
        _blobStorageService = blobStorageService;
        _metadataValidationService = metadataValidationService;
        _checksumService = checksumService;
        _tableStorageService = tableStorageService;
        _queueStorageService = queueStorageService;
        _logger = logger;
    }

    public async Task HandleBlobCreatedEventsAsync(
        IReadOnlyCollection<EventGridEventEnvelope<BlobCreatedEventData>> events,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(events);

        _logger.LogInformation(
            "Received {EventCount} blob-created intake event(s) for orchestration.",
            events.Count);

        foreach (var @event in events)
        {
            await HandleEventAsync(@event, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task HandleEventAsync(
        EventGridEventEnvelope<BlobCreatedEventData> @event,
        CancellationToken cancellationToken)
    {
        var blobName = BlobAddress.Parse(@event.Data.Url).BlobName;
        var metadata = await _blobStorageService
            .GetMetadataAsync(BlobContainerName.RawDocuments, blobName, cancellationToken)
            .ConfigureAwait(false);
        var validationResult = _metadataValidationService.ValidateRequiredMetadata(metadata);

        await using var contentStream = (await _blobStorageService
            .DownloadAsync(BlobContainerName.RawDocuments, blobName, cancellationToken)
            .ConfigureAwait(false))
            .ToStream();

        var checksum = await _checksumService
            .ComputeSha256Async(contentStream, cancellationToken)
            .ConfigureAwait(false);

        var intakeId = Guid.NewGuid().ToString("N");
        var correlationId = ResolveCorrelationId(validationResult.Metadata, @event);
        var routingDecision = CreateRoutingDecision(validationResult.IsValid);
        var routedBlob = await RouteBlobAsync(
                intakeId,
                correlationId,
                blobName,
                routingDecision,
                cancellationToken)
            .ConfigureAwait(false);

        var intakeRecord = new DocumentIntakeRecord
        {
            IntakeId = intakeId,
            EventId = @event.Id,
            CorrelationId = correlationId,
            BlobName = blobName,
            ContainerName = routedBlob.ContainerName,
            BlobUri = routedBlob.BlobUri,
            ContentType = @event.Data.ContentType,
            ContentLength = @event.Data.ContentLength,
            ETag = @event.Data.ETag,
            Checksum = checksum,
            Status = routingDecision.Status,
            ReceivedAtUtc = @event.EventTime,
            LastUpdatedAtUtc = @event.EventTime
        };

        await _tableStorageService
            .UpsertEntityAsync(
                TableName.DocumentIntake,
                DocumentIntakeTableEntity.FromRecord(
                    IntakeTablePartitionKey,
                    intakeRecord,
                    validationResult.ValidationErrors),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (validationResult.IsValid)
        {
            var queueMessage = new DocumentProcessingQueueMessage
            {
                IntakeId = intakeId,
                CorrelationId = correlationId,
                BlobName = blobName,
                ContainerName = routedBlob.ContainerName,
                BlobUri = routedBlob.BlobUri,
                ContentType = intakeRecord.ContentType,
                Checksum = checksum,
                EnqueuedAtUtc = @event.EventTime
            };

            await _queueStorageService
                .EnqueueAsync(BinaryData.FromObjectAsJson(queueMessage), cancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Validated intake {IntakeId} for blob {BlobName} with correlationId {CorrelationId}.",
                intakeId,
                blobName,
                correlationId);

            return;
        }

        _logger.LogWarning(
            "Quarantined intake {IntakeId} for blob {BlobName} with correlationId {CorrelationId}. Validation errors: {ValidationErrors}",
            intakeId,
            blobName,
            correlationId,
            JsonSerializer.Serialize(validationResult.ValidationErrors));
    }

    private static string ResolveCorrelationId(
        IReadOnlyDictionary<string, string> metadata,
        EventGridEventEnvelope<BlobCreatedEventData> @event)
    {
        if (metadata.TryGetValue(DocumentMetadataFieldNames.CorrelationId, out var correlationId)
            && !string.IsNullOrWhiteSpace(correlationId))
        {
            return correlationId;
        }

        return @event.Data.ClientRequestId
            ?? @event.Data.RequestId
            ?? @event.Id;
    }

    private BlobRoutingDecision CreateRoutingDecision(bool isValid) =>
        isValid
            ? new BlobRoutingDecision(BlobContainerName.ValidatedDocuments, DocumentIntakeStatus.Validated)
            : new BlobRoutingDecision(BlobContainerName.QuarantineDocuments, DocumentIntakeStatus.Quarantined);

    private async Task<RoutedBlob> RouteBlobAsync(
        string intakeId,
        string correlationId,
        string blobName,
        BlobRoutingDecision routingDecision,
        CancellationToken cancellationToken)
    {
        var sourceBlobUri = _blobStorageService.GetBlobUri(BlobContainerName.RawDocuments, blobName);
        var sourceBlob = BlobAddress.Parse(sourceBlobUri);

        _logger.LogInformation(
            "Routing intake {IntakeId} for blob {BlobName} with correlationId {CorrelationId} from {SourceContainer} to {DestinationContainer} using copy-based movement.",
            intakeId,
            blobName,
            correlationId,
            sourceBlob.ContainerName,
            routingDecision.DestinationContainer);

        await _blobStorageService
            .CopyAsync(
                BlobContainerName.RawDocuments,
                blobName,
                routingDecision.DestinationContainer,
                blobName,
                cancellationToken)
            .ConfigureAwait(false);
        await _blobStorageService
            .DeleteIfExistsAsync(BlobContainerName.RawDocuments, blobName, cancellationToken)
            .ConfigureAwait(false);

        var destinationBlobUri = _blobStorageService.GetBlobUri(routingDecision.DestinationContainer, blobName);
        var destinationBlob = BlobAddress.Parse(destinationBlobUri);

        _logger.LogInformation(
            "Completed routing for intake {IntakeId}. SourceBlobUri {SourceBlobUri}; DestinationBlobUri {DestinationBlobUri}; Status {Status}.",
            intakeId,
            sourceBlob.BlobUri,
            destinationBlob.BlobUri,
            routingDecision.Status);

        return new RoutedBlob(destinationBlob.ContainerName, destinationBlob.BlobUri);
    }

    private sealed record BlobRoutingDecision(
        BlobContainerName DestinationContainer,
        DocumentIntakeStatus Status);

    private sealed record RoutedBlob(
        string ContainerName,
        Uri BlobUri);

    private sealed record BlobAddress(string ContainerName, string BlobName, Uri BlobUri)
    {
        public static BlobAddress Parse(Uri blobUri)
        {
            var segments = blobUri.AbsolutePath
                .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (segments.Length < 3)
            {
                throw new InvalidOperationException(
                    $"Blob URL '{blobUri}' does not contain the expected account, container, and blob path.");
            }

            return new BlobAddress(
                segments[1],
                string.Join('/', segments.Skip(2)),
                blobUri);
        }

    }

    private sealed class DocumentIntakeTableEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;

        public string RowKey { get; set; } = string.Empty;

        public DateTimeOffset? Timestamp { get; set; }

        public ETag ETag { get; set; }

        public string IntakeId { get; set; } = string.Empty;

        public string EventId { get; set; } = string.Empty;

        public string CorrelationId { get; set; } = string.Empty;

        public string BlobName { get; set; } = string.Empty;

        public string ContainerName { get; set; } = string.Empty;

        public string BlobUri { get; set; } = string.Empty;

        public string? ContentType { get; set; }

        public long? ContentLength { get; set; }

        public string? SourceETag { get; set; }

        public string? Checksum { get; set; }

        public string Status { get; set; } = string.Empty;

        public DateTimeOffset ReceivedAtUtc { get; set; }

        public DateTimeOffset? LastUpdatedAtUtc { get; set; }

        public string? ValidationErrors { get; set; }

        public static DocumentIntakeTableEntity FromRecord(
            string partitionKey,
            DocumentIntakeRecord record,
            IReadOnlyList<string> validationErrors) =>
            new()
            {
                PartitionKey = partitionKey,
                RowKey = record.IntakeId,
                IntakeId = record.IntakeId,
                EventId = record.EventId,
                CorrelationId = record.CorrelationId,
                BlobName = record.BlobName,
                ContainerName = record.ContainerName,
                BlobUri = record.BlobUri.ToString(),
                ContentType = record.ContentType,
                ContentLength = record.ContentLength,
                SourceETag = record.ETag,
                Checksum = record.Checksum,
                Status = record.Status.ToString(),
                ReceivedAtUtc = record.ReceivedAtUtc,
                LastUpdatedAtUtc = record.LastUpdatedAtUtc,
                ValidationErrors = validationErrors.Count == 0
                    ? null
                    : string.Join(" | ", validationErrors)
            };
    }
}
