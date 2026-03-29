namespace RegulatoryIntake.Infrastructure.Configuration;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string ConnectionString { get; set; } = string.Empty;

    public BlobContainerOptions Containers { get; set; } = new();

    public QueueOptions Queues { get; set; } = new();

    public TableOptions Tables { get; set; } = new();

    public void Validate()
    {
        RequireValue(ConnectionString, $"{SectionName}:ConnectionString");
        Containers.Validate();
        Queues.Validate();
        Tables.Validate();
    }

    public sealed class BlobContainerOptions
    {
        public string RawDocuments { get; set; } = string.Empty;

        public string ValidatedDocuments { get; set; } = string.Empty;

        public string QuarantineDocuments { get; set; } = string.Empty;

        public void Validate()
        {
            RequireValue(RawDocuments, $"{SectionName}:Containers:RawDocuments");
            RequireValue(ValidatedDocuments, $"{SectionName}:Containers:ValidatedDocuments");
            RequireValue(QuarantineDocuments, $"{SectionName}:Containers:QuarantineDocuments");
        }
    }

    public sealed class QueueOptions
    {
        public string DocumentProcessing { get; set; } = string.Empty;

        public void Validate()
        {
            RequireValue(DocumentProcessing, $"{SectionName}:Queues:DocumentProcessing");
        }
    }

    public sealed class TableOptions
    {
        public string DocumentIntake { get; set; } = string.Empty;

        public string DocumentProcessing { get; set; } = string.Empty;

        public void Validate()
        {
            RequireValue(DocumentIntake, $"{SectionName}:Tables:DocumentIntake");
            RequireValue(DocumentProcessing, $"{SectionName}:Tables:DocumentProcessing");
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
