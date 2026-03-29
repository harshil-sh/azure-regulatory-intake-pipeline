namespace RegulatoryIntake.Domain.Entities;

public sealed record DocumentIntakeRecord
{
    public required string IntakeId { get; init; }

    public required string EventId { get; init; }

    public required string CorrelationId { get; init; }

    public required string BlobName { get; init; }

    public required string ContainerName { get; init; }

    public required Uri BlobUri { get; init; }

    public string? ContentType { get; init; }

    public long? ContentLength { get; init; }

    public string? ETag { get; init; }

    public string? Checksum { get; init; }

    public required DocumentIntakeStatus Status { get; init; }

    public required DateTimeOffset ReceivedAtUtc { get; init; }

    public DateTimeOffset? LastUpdatedAtUtc { get; init; }
}
