using Azure.Storage.Blobs;
using RegulatoryIntake.Application.Abstractions.Storage;
using RegulatoryIntake.Infrastructure.Configuration;

namespace RegulatoryIntake.Infrastructure.Services;

public sealed class AzureBlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly StorageOptions _storageOptions;

    public AzureBlobStorageService(
        BlobServiceClient blobServiceClient,
        Microsoft.Extensions.Options.IOptions<StorageOptions> storageOptions)
    {
        _blobServiceClient = blobServiceClient;
        _storageOptions = storageOptions.Value;
    }

    public async Task<BinaryData> DownloadAsync(
        BlobContainerName containerName,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        var blobClient = GetBlobClient(containerName, blobName);
        var response = await blobClient.DownloadContentAsync(cancellationToken).ConfigureAwait(false);
        return response.Value.Content;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetMetadataAsync(
        BlobContainerName containerName,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        var blobClient = GetBlobClient(containerName, blobName);
        var response = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        return response.Value.Metadata;
    }

    public async Task CopyAsync(
        BlobContainerName sourceContainerName,
        string sourceBlobName,
        BlobContainerName destinationContainerName,
        string destinationBlobName,
        CancellationToken cancellationToken = default)
    {
        var sourceBlobClient = GetBlobClient(sourceContainerName, sourceBlobName);
        var destinationContainerClient = _blobServiceClient.GetBlobContainerClient(ResolveContainerName(destinationContainerName));

        await destinationContainerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

        var destinationBlobClient = destinationContainerClient.GetBlobClient(destinationBlobName);
        var copyOperation = await destinationBlobClient
            .StartCopyFromUriAsync(sourceBlobClient.Uri, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        await copyOperation.WaitForCompletionAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteIfExistsAsync(
        BlobContainerName containerName,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        var blobClient = GetBlobClient(containerName, blobName);
        await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    public Uri GetBlobUri(BlobContainerName containerName, string blobName) =>
        GetBlobClient(containerName, blobName).Uri;

    private BlobClient GetBlobClient(BlobContainerName containerName, string blobName)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(ResolveContainerName(containerName));
        return containerClient.GetBlobClient(blobName);
    }

    private string ResolveContainerName(BlobContainerName containerName) => containerName switch
    {
        BlobContainerName.RawDocuments => _storageOptions.Containers.RawDocuments,
        BlobContainerName.ValidatedDocuments => _storageOptions.Containers.ValidatedDocuments,
        BlobContainerName.QuarantineDocuments => _storageOptions.Containers.QuarantineDocuments,
        _ => throw new ArgumentOutOfRangeException(nameof(containerName), containerName, "Unsupported blob container.")
    };
}
