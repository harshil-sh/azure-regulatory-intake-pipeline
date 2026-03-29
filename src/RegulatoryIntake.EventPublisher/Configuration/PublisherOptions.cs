using RegulatoryIntake.Domain.Metadata;

namespace RegulatoryIntake.EventPublisher.Configuration;

public sealed class PublisherOptions
{
    public const string SectionName = "Publisher";

    public StorageOptions Storage { get; set; } = new();

    public EventOptions Event { get; set; } = new();

    public SampleDocumentOptions SampleDocument { get; set; } = new();

    public void Validate()
    {
        Storage.Validate();
        Event.Validate();
        SampleDocument.Validate();
    }

    public sealed class StorageOptions
    {
        public string ConnectionString { get; set; } = string.Empty;

        public string RawDocumentsContainer { get; set; } = string.Empty;

        public void Validate()
        {
            RequireValue(ConnectionString, $"{SectionName}:Storage:ConnectionString");
            RequireValue(RawDocumentsContainer, $"{SectionName}:Storage:RawDocumentsContainer");
        }
    }

    public sealed class EventOptions
    {
        public string FunctionEndpoint { get; set; } = string.Empty;

        public string? FunctionCode { get; set; }

        public string Topic { get; set; } =
            "/subscriptions/local/resourceGroups/local/providers/Microsoft.Storage/storageAccounts/devstoreaccount1";

        public string EventType { get; set; } = "Microsoft.Storage.BlobCreated";

        public string DataVersion { get; set; } = "1";

        public string Api { get; set; } = "PutBlob";

        public void Validate()
        {
            RequireValue(FunctionEndpoint, $"{SectionName}:Event:FunctionEndpoint");
            RequireValue(Topic, $"{SectionName}:Event:Topic");
            RequireValue(EventType, $"{SectionName}:Event:EventType");
            RequireValue(DataVersion, $"{SectionName}:Event:DataVersion");
            RequireValue(Api, $"{SectionName}:Event:Api");

            if (!Uri.TryCreate(FunctionEndpoint, UriKind.Absolute, out var endpointUri)
                || (endpointUri.Scheme != Uri.UriSchemeHttp && endpointUri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException(
                    $"Configuration value '{SectionName}:Event:FunctionEndpoint' must be an absolute HTTP or HTTPS URI.");
            }
        }
    }

    public sealed class SampleDocumentOptions
    {
        public string FilePath { get; set; } = string.Empty;

        public string? BlobName { get; set; }

        public string? ContentType { get; set; }

        public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public void Validate()
        {
            RequireValue(FilePath, $"{SectionName}:SampleDocument:FilePath");

            foreach (var requiredField in DocumentMetadataFieldNames.RequiredFields)
            {
                if (!Metadata.TryGetValue(requiredField, out var value) || string.IsNullOrWhiteSpace(value))
                {
                    throw new InvalidOperationException(
                        $"Configuration value '{SectionName}:SampleDocument:Metadata:{requiredField}' is required.");
                }
            }
        }
    }

    private static void RequireValue(string? value, string configurationKey)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Configuration value '{configurationKey}' is required.");
        }
    }
}
