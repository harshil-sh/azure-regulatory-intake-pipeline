using Microsoft.Azure.Functions.Worker;
using RegulatoryIntake.Application.Abstractions;
using RegulatoryIntake.Functions.Functions;

namespace RegulatoryIntake.Functions.Tests;

public sealed class BlobCreatedEventIntakeFunctionTests
{
    [Fact]
    public void Parse_ReturnsEvents_ForValidEventGridArrayPayload()
    {
        const string payload = """
            [
              {
                "id": "evt-001",
                "topic": "/subscriptions/local/resourceGroups/dev/providers/Microsoft.Storage/storageAccounts/devstoreaccount1",
                "subject": "/blobServices/default/containers/raw-documents/blobs/regulatory-report.pdf",
                "eventType": "Microsoft.Storage.BlobCreated",
                "eventTime": "2026-03-29T10:15:30Z",
                "data": {
                  "api": "PutBlob",
                  "clientRequestId": "client-123",
                  "requestId": "request-456",
                  "eTag": "0x8DB3A3C4D5E6F70",
                  "contentType": "application/pdf",
                  "contentLength": 2048,
                  "url": "http://127.0.0.1:10000/devstoreaccount1/raw-documents/regulatory-report.pdf",
                  "blobType": "BlockBlob",
                  "sequencer": "000000000000000000000000000000010000000000000001"
                },
                "dataVersion": "1",
                "metadataVersion": "1"
              }
            ]
            """;

        var result = BlobCreatedEventPayloadParser.Parse(payload);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Events);

        var parsedEvent = Assert.Single(result.Events);
        Assert.Equal("evt-001", parsedEvent.Id);
        Assert.Equal("Microsoft.Storage.BlobCreated", parsedEvent.EventType);
        Assert.Equal("PutBlob", parsedEvent.Data.Api);
        Assert.Equal(
            new Uri("http://127.0.0.1:10000/devstoreaccount1/raw-documents/regulatory-report.pdf"),
            parsedEvent.Data.Url);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_ReturnsFailure_ForEmptyPayload(string payload)
    {
        var result = BlobCreatedEventPayloadParser.Parse(payload);

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Events);
        Assert.Equal("Request body must contain an Event Grid-style payload.", result.ErrorMessage);
    }

    [Fact]
    public void Parse_ReturnsFailure_ForInvalidJson()
    {
        var result = BlobCreatedEventPayloadParser.Parse("{ not-json");

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Events);
        Assert.Equal("Payload is not valid JSON.", result.ErrorMessage);
    }

    [Fact]
    public void Parse_ReturnsFailure_ForInvalidEventGridEvent()
    {
        const string payload = """
            [
              {
                "id": "evt-002",
                "topic": "/subscriptions/local/resourceGroups/dev/providers/Microsoft.Storage/storageAccounts/devstoreaccount1",
                "subject": "/blobServices/default/containers/raw-documents/blobs/missing-api.pdf",
                "eventType": "Microsoft.Storage.BlobCreated",
                "eventTime": "2026-03-29T10:15:30Z",
                "data": {
                  "url": "http://127.0.0.1:10000/devstoreaccount1/raw-documents/missing-api.pdf"
                },
                "dataVersion": "1"
              }
            ]
            """;

        var result = BlobCreatedEventPayloadParser.Parse(payload);

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Events);
        Assert.Equal(
            "Payload must contain one or more valid blob-created Event Grid-style events.",
            result.ErrorMessage);
    }

    [Fact]
    public void RunAsync_HasExpectedHttpTriggerConfiguration()
    {
        var method = typeof(BlobCreatedEventIntakeFunction).GetMethod(nameof(BlobCreatedEventIntakeFunction.RunAsync));

        Assert.NotNull(method);

        var requestParameter = method!.GetParameters().Single(parameter => parameter.ParameterType.Name == "HttpRequestData");
        var triggerAttribute = Assert.IsType<HttpTriggerAttribute>(
            requestParameter.GetCustomAttributes(typeof(HttpTriggerAttribute), inherit: false).Single());

        Assert.Equal(AuthorizationLevel.Function, triggerAttribute.AuthLevel);
        Assert.Equal("intake/events/blob-created", triggerAttribute.Route);
        Assert.Contains("post", triggerAttribute.Methods, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Function_Constructor_RequiresIntakeOrchestrator()
    {
        var constructor = typeof(BlobCreatedEventIntakeFunction).GetConstructors().Single();

        Assert.Contains(
            constructor.GetParameters(),
            parameter => parameter.ParameterType == typeof(IIntakeOrchestrator));
    }

    [Fact]
    public void DocumentProcessingFunction_TryParseMessage_ReturnsMessage_ForValidPayload()
    {
        const string payload = """
            {
              "intakeId": "intake-001",
              "correlationId": "corr-123",
              "blobName": "regulatory-report.pdf",
              "containerName": "validated-documents",
              "blobUri": "http://127.0.0.1:10000/devstoreaccount1/validated-documents/regulatory-report.pdf",
              "contentType": "application/pdf",
              "checksum": "abc123",
              "enqueuedAtUtc": "2026-03-29T10:15:30Z"
            }
            """;

        var result = DocumentProcessingFunction.TryParseMessage(payload, out var message);

        Assert.True(result);
        Assert.NotNull(message);
        Assert.Equal("intake-001", message!.IntakeId);
        Assert.Equal("corr-123", message.CorrelationId);
        Assert.Equal("validated-documents", message.ContainerName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{ not-json")]
    public void DocumentProcessingFunction_TryParseMessage_ReturnsFalse_ForInvalidPayload(string payload)
    {
        var result = DocumentProcessingFunction.TryParseMessage(payload, out var message);

        Assert.False(result);
        Assert.Null(message);
    }

    [Fact]
    public void DocumentProcessingFunction_RunAsync_HasExpectedQueueTriggerConfiguration()
    {
        var method = typeof(DocumentProcessingFunction).GetMethod(nameof(DocumentProcessingFunction.RunAsync));

        Assert.NotNull(method);

        var messageParameter = method!.GetParameters().Single(parameter => parameter.ParameterType == typeof(string));
        var triggerAttribute = Assert.IsType<QueueTriggerAttribute>(
            messageParameter.GetCustomAttributes(typeof(QueueTriggerAttribute), inherit: false).Single());

        Assert.Equal("%Storage__Queues__DocumentProcessing%", triggerAttribute.QueueName);
        Assert.Equal("AzureWebJobsStorage", triggerAttribute.Connection);
    }

    [Fact]
    public void DocumentProcessingFunction_Constructor_RequiresDocumentProcessingService()
    {
        var constructor = typeof(DocumentProcessingFunction).GetConstructors().Single();

        Assert.Contains(
            constructor.GetParameters(),
            parameter => parameter.ParameterType == typeof(IDocumentProcessingService));
    }
}
