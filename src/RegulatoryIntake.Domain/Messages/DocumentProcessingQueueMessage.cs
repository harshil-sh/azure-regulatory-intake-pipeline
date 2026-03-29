namespace RegulatoryIntake.Domain.Messages;

public sealed record DocumentProcessingQueueMessage
{
    public required string IntakeId { get; init; }

    public required string CorrelationId { get; init; }

    public required string BlobName { get; init; }

    public required string ContainerName { get; init; }

    public required Uri BlobUri { get; init; }

    public string? ContentType { get; init; }

    public string? Checksum { get; init; }

    public required DateTimeOffset EnqueuedAtUtc { get; init; }
}
