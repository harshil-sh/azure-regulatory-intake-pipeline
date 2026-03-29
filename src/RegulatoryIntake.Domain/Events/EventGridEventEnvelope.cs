namespace RegulatoryIntake.Domain.Events;

public sealed record EventGridEventEnvelope<TData>
{
    public required string Id { get; init; }

    public required string Topic { get; init; }

    public required string Subject { get; init; }

    public required string EventType { get; init; }

    public required DateTimeOffset EventTime { get; init; }

    public required TData Data { get; init; }

    public required string DataVersion { get; init; }

    public string? MetadataVersion { get; init; }
}
