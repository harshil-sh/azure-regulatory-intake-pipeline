namespace RegulatoryIntake.Domain.Entities;

public sealed record DocumentProcessingRecord
{
    public required string IntakeId { get; init; }

    public required string CorrelationId { get; init; }

    public required string BlobName { get; init; }

    public required string ContainerName { get; init; }

    public required Uri BlobUri { get; init; }

    public string? ContentType { get; init; }

    public string? Checksum { get; init; }

    public required DocumentProcessingStatus Status { get; init; }

    public required DateTimeOffset EnqueuedAtUtc { get; init; }

    public required DateTimeOffset ProcessedAtUtc { get; init; }
}
