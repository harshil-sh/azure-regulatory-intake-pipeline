namespace RegulatoryIntake.Application.Abstractions;

public interface IChecksumService
{
    ValueTask<string> ComputeSha256Async(Stream content, CancellationToken cancellationToken = default);
}
