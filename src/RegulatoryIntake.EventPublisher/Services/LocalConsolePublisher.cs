using System.Net.Http.Json;
using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RegulatoryIntake.Domain.Events;
using RegulatoryIntake.Domain.Metadata;
using RegulatoryIntake.EventPublisher.Configuration;

namespace RegulatoryIntake.EventPublisher.Services;

internal sealed class LocalConsolePublisher
{
    private static readonly JsonSerializerOptions EventSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly BlobServiceClient _blobServiceClient;
    private readonly HttpClient _httpClient;
    private readonly PublisherOptions _options;
    private readonly ILogger<LocalConsolePublisher> _logger;

    public LocalConsolePublisher(
        BlobServiceClient blobServiceClient,
        HttpClient httpClient,
        IOptions<PublisherOptions> options,
        ILogger<LocalConsolePublisher> logger)
    {
        _blobServiceClient = blobServiceClient;
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        var sampleFilePath = Path.GetFullPath(_options.SampleDocument.FilePath, Directory.GetCurrentDirectory());
        if (!File.Exists(sampleFilePath))
        {
            throw new FileNotFoundException(
                $"Sample document '{sampleFilePath}' does not exist. Update {PublisherOptions.SectionName}:SampleDocument:FilePath.",
                sampleFilePath);
        }

        var blobName = string.IsNullOrWhiteSpace(_options.SampleDocument.BlobName)
            ? Path.GetFileName(sampleFilePath)
            : _options.SampleDocument.BlobName.Trim();
        var contentType = ResolveContentType(sampleFilePath, _options.SampleDocument.ContentType);

        var containerClient = _blobServiceClient.GetBlobContainerClient(_options.Storage.RawDocumentsContainer);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        var blobClient = containerClient.GetBlobClient(blobName);
        await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        await using (var stream = File.OpenRead(sampleFilePath))
        {
            await blobClient.UploadAsync(
                    stream,
                    new BlobUploadOptions
                    {
                        HttpHeaders = new BlobHttpHeaders
                        {
                            ContentType = contentType
                        },
                        Metadata = NormalizeMetadata(_options.SampleDocument.Metadata)
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var eventEnvelope = CreateBlobCreatedEvent(blobClient.Uri, blobName, contentType, new FileInfo(sampleFilePath).Length);
        var functionEndpoint = BuildFunctionEndpoint();

        _logger.LogInformation(
            "Uploaded {BlobName} to container {ContainerName}. Posting event to {FunctionEndpoint}.",
            blobName,
            _options.Storage.RawDocumentsContainer,
            functionEndpoint);

        using var response = await _httpClient
            .PostAsJsonAsync(functionEndpoint, new[] { eventEnvelope }, EventSerializerOptions, cancellationToken)
            .ConfigureAwait(false);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Function endpoint returned {(int)response.StatusCode} {response.ReasonPhrase}. Response body: {responseBody}");
        }

        _logger.LogInformation(
            "Posted blob-created event for {BlobName}. Function response: {StatusCode} {ResponseBody}",
            blobName,
            (int)response.StatusCode,
            string.IsNullOrWhiteSpace(responseBody) ? "<empty>" : responseBody);

        return 0;
    }

    private EventGridEventEnvelope<BlobCreatedEventData> CreateBlobCreatedEvent(
        Uri blobUri,
        string blobName,
        string contentType,
        long contentLength)
    {
        var correlationId = _options.SampleDocument.Metadata[DocumentMetadataFieldNames.CorrelationId];

        return new EventGridEventEnvelope<BlobCreatedEventData>
        {
            Id = Guid.NewGuid().ToString("N"),
            Topic = _options.Event.Topic,
            Subject = $"/blobServices/default/containers/{_options.Storage.RawDocumentsContainer}/blobs/{blobName}",
            EventType = _options.Event.EventType,
            EventTime = DateTimeOffset.UtcNow,
            DataVersion = _options.Event.DataVersion,
            MetadataVersion = "1",
            Data = new BlobCreatedEventData
            {
                Api = _options.Event.Api,
                ClientRequestId = correlationId,
                ContentLength = contentLength,
                ContentType = contentType,
                Url = blobUri,
                BlobType = "BlockBlob"
            }
        };
    }

    private Uri BuildFunctionEndpoint()
    {
        var builder = new UriBuilder(_options.Event.FunctionEndpoint);
        if (string.IsNullOrWhiteSpace(_options.Event.FunctionCode))
        {
            return builder.Uri;
        }

        var queryPrefix = string.IsNullOrWhiteSpace(builder.Query) ? string.Empty : builder.Query.TrimStart('?') + "&";
        builder.Query = $"{queryPrefix}code={Uri.EscapeDataString(_options.Event.FunctionCode)}";
        return builder.Uri;
    }

    private static Dictionary<string, string> NormalizeMetadata(IReadOnlyDictionary<string, string> metadata)
    {
        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in metadata)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            normalized[key.Trim()] = value.Trim();
        }

        return normalized;
    }

    private static string ResolveContentType(string sampleFilePath, string? configuredContentType)
    {
        if (!string.IsNullOrWhiteSpace(configuredContentType))
        {
            return configuredContentType.Trim();
        }

        return Path.GetExtension(sampleFilePath).ToLowerInvariant() switch
        {
            ".json" => "application/json",
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            ".xml" => "application/xml",
            _ => "application/octet-stream"
        };
    }
}
