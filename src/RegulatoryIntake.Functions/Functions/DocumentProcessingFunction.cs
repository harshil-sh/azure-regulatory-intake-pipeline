using System.Text.Json;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RegulatoryIntake.Application.Abstractions;
using RegulatoryIntake.Domain.Messages;

namespace RegulatoryIntake.Functions.Functions;

public sealed class DocumentProcessingFunction
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IDocumentProcessingService _documentProcessingService;
    private readonly ILogger<DocumentProcessingFunction> _logger;

    public DocumentProcessingFunction(
        IDocumentProcessingService documentProcessingService,
        ILogger<DocumentProcessingFunction> logger)
    {
        _documentProcessingService = documentProcessingService;
        _logger = logger;
    }

    [Function(nameof(DocumentProcessingFunction))]
    public async Task RunAsync(
        [QueueTrigger("%Storage__Queues__DocumentProcessing%", Connection = "AzureWebJobsStorage")]
        string queueMessagePayload,
        CancellationToken cancellationToken)
    {
        if (!TryParseMessage(queueMessagePayload, out var message))
        {
            _logger.LogError(
                "Document processing queue message could not be parsed. PayloadLength={PayloadLength}",
                queueMessagePayload?.Length ?? 0);

            throw new InvalidOperationException("Document processing queue message is invalid.");
        }

        _logger.LogInformation(
            "Starting downstream processing for intake {IntakeId}, blob {BlobName}, correlationId {CorrelationId}.",
            message!.IntakeId,
            message.BlobName,
            message.CorrelationId);

        await _documentProcessingService.ProcessAsync(message, cancellationToken).ConfigureAwait(false);
    }

    public static bool TryParseMessage(
        string queueMessagePayload,
        [NotNullWhen(true)]
        out DocumentProcessingQueueMessage? message)
    {
        message = null;

        if (string.IsNullOrWhiteSpace(queueMessagePayload))
        {
            return false;
        }

        try
        {
            message = JsonSerializer.Deserialize<DocumentProcessingQueueMessage>(
                queueMessagePayload,
                SerializerOptions);
        }
        catch (JsonException)
        {
            return false;
        }

        return message is not null;
    }
}
