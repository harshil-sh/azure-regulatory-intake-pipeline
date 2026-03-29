namespace RegulatoryIntake.Application.Models;

public sealed record MetadataValidationResult
{
    private MetadataValidationResult(
        bool isValid,
        IReadOnlyDictionary<string, string> metadata,
        IReadOnlyList<string> validationErrors)
    {
        IsValid = isValid;
        Metadata = metadata;
        ValidationErrors = validationErrors;
    }

    public bool IsValid { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }

    public IReadOnlyList<string> ValidationErrors { get; }

    public static MetadataValidationResult Success(IReadOnlyDictionary<string, string> metadata) =>
        new(true, metadata, Array.Empty<string>());

    public static MetadataValidationResult Failure(
        IReadOnlyDictionary<string, string> metadata,
        IReadOnlyList<string> validationErrors) =>
        new(false, metadata, validationErrors);
}
