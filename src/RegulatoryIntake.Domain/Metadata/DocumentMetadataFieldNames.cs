namespace RegulatoryIntake.Domain.Metadata;

public static class DocumentMetadataFieldNames
{
    public const string CorrelationId = "correlationId";

    public const string DocumentType = "documentType";

    public const string SourceSystem = "sourceSystem";

    public static IReadOnlyList<string> RequiredFields { get; } =
    [
        CorrelationId,
        DocumentType,
        SourceSystem
    ];
}
