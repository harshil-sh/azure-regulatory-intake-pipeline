using RegulatoryIntake.Application.Services;
using RegulatoryIntake.Domain.Metadata;

namespace RegulatoryIntake.Application.Tests;

public sealed class MetadataValidationServiceTests
{
    private readonly MetadataValidationService _service = new();

    [Fact]
    public void ValidateRequiredMetadata_ReturnsSuccess_WhenAllRequiredFieldsArePresent()
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [" correlationId "] = " corr-123 ",
            ["DOCUMENTTYPE"] = " policy ",
            ["sourceSystem"] = " portal ",
            [" optionalField "] = " optional-value "
        };

        var result = _service.ValidateRequiredMetadata(metadata);

        Assert.True(result.IsValid);
        Assert.Empty(result.ValidationErrors);
        Assert.Equal("corr-123", result.Metadata[DocumentMetadataFieldNames.CorrelationId]);
        Assert.Equal("policy", result.Metadata[DocumentMetadataFieldNames.DocumentType]);
        Assert.Equal("portal", result.Metadata[DocumentMetadataFieldNames.SourceSystem]);
        Assert.Equal("optional-value", result.Metadata["optionalField"]);
    }

    [Fact]
    public void ValidateRequiredMetadata_ReturnsFailure_WhenMetadataIsMissingOrWhitespace()
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [DocumentMetadataFieldNames.CorrelationId] = " ",
            [DocumentMetadataFieldNames.DocumentType] = "policy"
        };

        var result = _service.ValidateRequiredMetadata(metadata);

        Assert.False(result.IsValid);
        Assert.Equal(2, result.ValidationErrors.Count);
        Assert.Contains("Metadata field 'correlationId' is required.", result.ValidationErrors);
        Assert.Contains("Metadata field 'sourceSystem' is required.", result.ValidationErrors);
    }

    [Fact]
    public void ValidateRequiredMetadata_IgnoresBlankKeys()
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [" "] = "ignored",
            [DocumentMetadataFieldNames.CorrelationId] = "corr-123",
            [DocumentMetadataFieldNames.DocumentType] = "policy",
            [DocumentMetadataFieldNames.SourceSystem] = "portal"
        };

        var result = _service.ValidateRequiredMetadata(metadata);

        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Metadata.Keys, string.IsNullOrWhiteSpace);
    }
}
