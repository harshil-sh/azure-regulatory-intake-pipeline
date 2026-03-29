using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RegulatoryIntake.Application.Abstractions;

namespace RegulatoryIntake.Functions.Functions;

public sealed class BlobCreatedEventIntakeFunction
{
    private readonly IIntakeOrchestrator _intakeOrchestrator;
    private readonly ILogger<BlobCreatedEventIntakeFunction> _logger;

    public BlobCreatedEventIntakeFunction(
        IIntakeOrchestrator intakeOrchestrator,
        ILogger<BlobCreatedEventIntakeFunction> logger)
    {
        _intakeOrchestrator = intakeOrchestrator;
        _logger = logger;
    }

    [Function(nameof(BlobCreatedEventIntakeFunction))]
    public async Task<HttpResponseData> RunAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "intake/events/blob-created")]
        HttpRequestData request,
        CancellationToken cancellationToken)
    {
        string requestBody;
        using (var reader = new StreamReader(request.Body, leaveOpen: true))
        {
            requestBody = await reader.ReadToEndAsync(cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(requestBody))
        {
            return await CreateBadRequestAsync(
                request,
                "Request body must contain an Event Grid-style payload.",
                cancellationToken);
        }

        if (!TryParseEvents(requestBody, out var events, out var errorMessage))
        {
            return await CreateBadRequestAsync(request, errorMessage, cancellationToken);
        }

        await _intakeOrchestrator.HandleBlobCreatedEventsAsync(events, cancellationToken);

        _logger.LogInformation(
            "Accepted {EventCount} blob-created Event Grid-style event(s).",
            events.Count);

        var response = request.CreateResponse(HttpStatusCode.Accepted);
        await response.WriteStringAsync("Accepted.", cancellationToken);
        return response;
    }

    private static bool TryParseEvents(
        string requestBody,
        out IReadOnlyCollection<RegulatoryIntake.Domain.Events.EventGridEventEnvelope<RegulatoryIntake.Domain.Events.BlobCreatedEventData>> events,
        out string errorMessage)
    {
        var result = BlobCreatedEventPayloadParser.Parse(requestBody);
        events = result.Events;
        errorMessage = result.ErrorMessage;
        return result.IsSuccess;
    }

    private static async Task<HttpResponseData> CreateBadRequestAsync(
        HttpRequestData request,
        string message,
        CancellationToken cancellationToken)
    {
        var response = request.CreateResponse(HttpStatusCode.BadRequest);
        await response.WriteStringAsync(message, cancellationToken);
        return response;
    }
}
