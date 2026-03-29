using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Logging;
using RegulatoryIntake.Application.Abstractions;
using RegulatoryIntake.Application.Abstractions.Storage;
using RegulatoryIntake.Domain.Entities;
using RegulatoryIntake.Domain.Messages;

namespace RegulatoryIntake.Application.Services;

public sealed class DocumentProcessingService : IDocumentProcessingService
{
    private const string ProcessingTablePartitionKey = "DocumentProcessing";

    private readonly ITableStorageService _tableStorageService;
    private readonly ILogger<DocumentProcessingService> _logger;

    public DocumentProcessingService(
        ITableStorageService tableStorageService,
        ILogger<DocumentProcessingService> logger)
    {
        _tableStorageService = tableStorageService;
        _logger = logger;
    }

    public async Task ProcessAsync(
        DocumentProcessingQueueMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var processedAtUtc = DateTimeOffset.UtcNow;
        var record = new DocumentProcessingRecord
        {
            IntakeId = message.IntakeId,
            CorrelationId = message.CorrelationId,
            BlobName = message.BlobName,
            ContainerName = message.ContainerName,
            BlobUri = message.BlobUri,
            ContentType = message.ContentType,
            Checksum = message.Checksum,
            Status = DocumentProcessingStatus.Completed,
            EnqueuedAtUtc = message.EnqueuedAtUtc,
            ProcessedAtUtc = processedAtUtc
        };

        await _tableStorageService
            .UpsertEntityAsync(
                TableName.DocumentProcessing,
                DocumentProcessingTableEntity.FromRecord(ProcessingTablePartitionKey, record),
                cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Completed downstream processing for intake {IntakeId}, blob {BlobName}, correlationId {CorrelationId}.",
            message.IntakeId,
            message.BlobName,
            message.CorrelationId);
    }

    private sealed class DocumentProcessingTableEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;

        public string RowKey { get; set; } = string.Empty;

        public DateTimeOffset? Timestamp { get; set; }

        public ETag ETag { get; set; }

        public string IntakeId { get; set; } = string.Empty;

        public string CorrelationId { get; set; } = string.Empty;

        public string BlobName { get; set; } = string.Empty;

        public string ContainerName { get; set; } = string.Empty;

        public string BlobUri { get; set; } = string.Empty;

        public string? ContentType { get; set; }

        public string? Checksum { get; set; }

        public string Status { get; set; } = string.Empty;

        public DateTimeOffset EnqueuedAtUtc { get; set; }

        public DateTimeOffset ProcessedAtUtc { get; set; }

        public static DocumentProcessingTableEntity FromRecord(
            string partitionKey,
            DocumentProcessingRecord record) =>
            new()
            {
                PartitionKey = partitionKey,
                RowKey = record.IntakeId,
                IntakeId = record.IntakeId,
                CorrelationId = record.CorrelationId,
                BlobName = record.BlobName,
                ContainerName = record.ContainerName,
                BlobUri = record.BlobUri.ToString(),
                ContentType = record.ContentType,
                Checksum = record.Checksum,
                Status = record.Status.ToString(),
                EnqueuedAtUtc = record.EnqueuedAtUtc,
                ProcessedAtUtc = record.ProcessedAtUtc
            };
    }
}
