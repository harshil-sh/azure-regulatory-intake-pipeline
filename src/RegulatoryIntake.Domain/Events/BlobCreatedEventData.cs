namespace RegulatoryIntake.Domain.Events;

public sealed record BlobCreatedEventData
{
    public required string Api { get; init; }

    public string? ClientRequestId { get; init; }

    public string? RequestId { get; init; }

    public string? ETag { get; init; }

    public string? ContentType { get; init; }

    public long? ContentLength { get; init; }

    public required Uri Url { get; init; }

    public string? BlobType { get; init; }

    public string? Sequencer { get; init; }
}
