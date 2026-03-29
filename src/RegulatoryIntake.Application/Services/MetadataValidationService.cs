using RegulatoryIntake.Application.Abstractions;
using RegulatoryIntake.Application.Models;
using RegulatoryIntake.Domain.Metadata;

namespace RegulatoryIntake.Application.Services;

public sealed class MetadataValidationService : IMetadataValidationService
{
    public MetadataValidationResult ValidateRequiredMetadata(IReadOnlyDictionary<string, string>? metadata)
    {
        var normalizedMetadata = NormalizeMetadata(metadata);
        var validationErrors = new List<string>();

        foreach (var requiredField in DocumentMetadataFieldNames.RequiredFields)
        {
            if (!normalizedMetadata.TryGetValue(requiredField, out var value) || string.IsNullOrWhiteSpace(value))
            {
                validationErrors.Add($"Metadata field '{requiredField}' is required.");
            }
        }

        return validationErrors.Count == 0
            ? MetadataValidationResult.Success(normalizedMetadata)
            : MetadataValidationResult.Failure(normalizedMetadata, validationErrors);
    }

    private static IReadOnlyDictionary<string, string> NormalizeMetadata(IReadOnlyDictionary<string, string>? metadata)
    {
        var normalizedMetadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (metadata is null)
        {
            return normalizedMetadata;
        }

        foreach (var (key, value) in metadata)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            normalizedMetadata[key.Trim()] = value.Trim();
        }

        return normalizedMetadata;
    }
}
