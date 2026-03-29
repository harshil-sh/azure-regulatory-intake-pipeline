using System.Text;
using RegulatoryIntake.Application.Services;

namespace RegulatoryIntake.Application.Tests;

public sealed class Sha256ChecksumServiceTests
{
    private readonly Sha256ChecksumService _service = new();

    [Fact]
    public async Task ComputeSha256Async_ReturnsExpectedChecksum_ForKnownContent()
    {
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("hello world"));

        var checksum = await _service.ComputeSha256Async(content);

        Assert.Equal(
            "B94D27B9934D3E08A52E52D7DA7DABFAC484EFE37A5380EE9088F7ACE2EFCDE9",
            checksum);
    }

    [Fact]
    public async Task ComputeSha256Async_ReturnsSameChecksum_ForEquivalentStreams()
    {
        await using var first = new MemoryStream(Encoding.UTF8.GetBytes("regulatory-document"));
        await using var second = new MemoryStream(Encoding.UTF8.GetBytes("regulatory-document"));

        var firstChecksum = await _service.ComputeSha256Async(first);
        var secondChecksum = await _service.ComputeSha256Async(second);

        Assert.Equal(firstChecksum, secondChecksum);
    }

    [Fact]
    public async Task ComputeSha256Async_Throws_WhenStreamIsNotReadable()
    {
        await using var content = new WriteOnlyMemoryStream();

        var exception = await Assert.ThrowsAsync<ArgumentException>(async () => await _service.ComputeSha256Async(content));

        Assert.Equal("content", exception.ParamName);
    }

    private sealed class WriteOnlyMemoryStream : MemoryStream
    {
        public override bool CanRead => false;

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override int Read(Span<byte> buffer) =>
            throw new NotSupportedException();

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(new NotSupportedException());

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            Task.FromException<int>(new NotSupportedException());
    }
}
