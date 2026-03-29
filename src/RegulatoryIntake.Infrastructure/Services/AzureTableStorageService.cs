using Azure.Data.Tables;
using RegulatoryIntake.Application.Abstractions.Storage;
using RegulatoryIntake.Infrastructure.Configuration;

namespace RegulatoryIntake.Infrastructure.Services;

public sealed class AzureTableStorageService : ITableStorageService
{
    private readonly TableServiceClient _tableServiceClient;
    private readonly StorageOptions _storageOptions;

    public AzureTableStorageService(
        TableServiceClient tableServiceClient,
        Microsoft.Extensions.Options.IOptions<StorageOptions> storageOptions)
    {
        _tableServiceClient = tableServiceClient;
        _storageOptions = storageOptions.Value;
    }

    public async Task UpsertEntityAsync<T>(
        TableName tableName,
        T entity,
        TableUpdateMode updateMode = TableUpdateMode.Replace,
        CancellationToken cancellationToken = default)
        where T : class, ITableEntity
    {
        var tableClient = _tableServiceClient.GetTableClient(ResolveTableName(tableName));
        await tableClient.CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
        await tableClient.UpsertEntityAsync(entity, updateMode, cancellationToken).ConfigureAwait(false);
    }

    private string ResolveTableName(TableName tableName) => tableName switch
    {
        TableName.DocumentIntake => _storageOptions.Tables.DocumentIntake,
        TableName.DocumentProcessing => _storageOptions.Tables.DocumentProcessing,
        _ => throw new ArgumentOutOfRangeException(nameof(tableName), tableName, "Unsupported table.")
    };
}
