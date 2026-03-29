namespace RegulatoryIntake.Application.Abstractions.Storage;

public interface IBlobStorageService
{
    Task<BinaryData> DownloadAsync(
        BlobContainerName containerName,
        string blobName,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, string>> GetMetadataAsync(
        BlobContainerName containerName,
        string blobName,
        CancellationToken cancellationToken = default);

    Task CopyAsync(
        BlobContainerName sourceContainerName,
        string sourceBlobName,
        BlobContainerName destinationContainerName,
        string destinationBlobName,
        CancellationToken cancellationToken = default);

    Task DeleteIfExistsAsync(
        BlobContainerName containerName,
        string blobName,
        CancellationToken cancellationToken = default);
}
