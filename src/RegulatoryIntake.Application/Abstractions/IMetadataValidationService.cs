using RegulatoryIntake.Application.Models;

namespace RegulatoryIntake.Application.Abstractions;

public interface IMetadataValidationService
{
    MetadataValidationResult ValidateRequiredMetadata(IReadOnlyDictionary<string, string>? metadata);
}
