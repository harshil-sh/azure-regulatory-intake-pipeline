using System.Text.Json;
using RegulatoryIntake.Domain.Events;

namespace RegulatoryIntake.Functions.Functions;

public static class BlobCreatedEventPayloadParser
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static BlobCreatedEventPayloadParseResult Parse(string requestBody)
    {
        if (string.IsNullOrWhiteSpace(requestBody))
        {
            return BlobCreatedEventPayloadParseResult.Failure(
                "Request body must contain an Event Grid-style payload.");
        }

        try
        {
            using var document = JsonDocument.Parse(requestBody);

            List<EventGridEventEnvelope<BlobCreatedEventData>>? parsedEvents = document.RootElement.ValueKind switch
            {
                JsonValueKind.Array => JsonSerializer.Deserialize<List<EventGridEventEnvelope<BlobCreatedEventData>>>(
                    requestBody,
                    SerializerOptions),
                JsonValueKind.Object => ParseSingleEvent(document.RootElement),
                _ => null
            };

            if (parsedEvents is null || parsedEvents.Count == 0 || parsedEvents.Any(IsInvalidEvent))
            {
                return BlobCreatedEventPayloadParseResult.Failure(
                    "Payload must contain one or more valid blob-created Event Grid-style events.");
            }

            return BlobCreatedEventPayloadParseResult.Success(parsedEvents);
        }
        catch (JsonException)
        {
            return BlobCreatedEventPayloadParseResult.Failure("Payload is not valid JSON.");
        }
    }

    private static List<EventGridEventEnvelope<BlobCreatedEventData>>? ParseSingleEvent(JsonElement rootElement)
    {
        var parsedEvent = rootElement.Deserialize<EventGridEventEnvelope<BlobCreatedEventData>>(SerializerOptions);
        return parsedEvent is null ? null : [parsedEvent];
    }

    private static bool IsInvalidEvent(EventGridEventEnvelope<BlobCreatedEventData> @event)
    {
        return string.IsNullOrWhiteSpace(@event.Id)
            || string.IsNullOrWhiteSpace(@event.Topic)
            || string.IsNullOrWhiteSpace(@event.Subject)
            || string.IsNullOrWhiteSpace(@event.EventType)
            || string.IsNullOrWhiteSpace(@event.DataVersion)
            || @event.Data is null
            || @event.Data.Url is null
            || string.IsNullOrWhiteSpace(@event.Data.Api);
    }
}

public sealed record BlobCreatedEventPayloadParseResult
{
    public static BlobCreatedEventPayloadParseResult Success(
        IReadOnlyCollection<EventGridEventEnvelope<BlobCreatedEventData>> events) =>
        new(true, events, string.Empty);

    public static BlobCreatedEventPayloadParseResult Failure(string errorMessage) =>
        new(false, Array.Empty<EventGridEventEnvelope<BlobCreatedEventData>>(), errorMessage);

    private BlobCreatedEventPayloadParseResult(
        bool isSuccess,
        IReadOnlyCollection<EventGridEventEnvelope<BlobCreatedEventData>> events,
        string errorMessage)
    {
        IsSuccess = isSuccess;
        Events = events;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }

    public IReadOnlyCollection<EventGridEventEnvelope<BlobCreatedEventData>> Events { get; }

    public string ErrorMessage { get; }
}
