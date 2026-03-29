using System.Security.Cryptography;
using RegulatoryIntake.Application.Abstractions;

namespace RegulatoryIntake.Application.Services;

public sealed class Sha256ChecksumService : IChecksumService
{
    public async ValueTask<string> ComputeSha256Async(Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (!content.CanRead)
        {
            throw new ArgumentException("The content stream must be readable.", nameof(content));
        }

        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(content, cancellationToken);
        return Convert.ToHexString(hash);
    }
}
