using Azure.Data.Tables;

namespace RegulatoryIntake.Application.Abstractions.Storage;

public interface ITableStorageService
{
    Task UpsertEntityAsync<T>(
        TableName tableName,
        T entity,
        TableUpdateMode updateMode = TableUpdateMode.Replace,
        CancellationToken cancellationToken = default)
        where T : class, ITableEntity;
}
