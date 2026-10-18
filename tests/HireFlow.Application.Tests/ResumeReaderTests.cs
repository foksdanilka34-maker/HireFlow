using System.Security.Cryptography;
using System.Text;
using HireFlow.Application.Common;
using HireFlow.Application.Resumes;

namespace HireFlow.Application.Tests;

public class ResumeReaderTests
{
    private static readonly byte[] PdfSignature = [0x25, 0x50, 0x44, 0x46];
    private static readonly byte[] ZipSignature = [0x50, 0x4B, 0x03, 0x04];
    private static readonly byte[] OleSignature = [0xD0, 0xCF, 0x11, 0xE0];

    // Самописный фейк: запоминает вызовы и возвращает заданный текст или бросает заданную ошибку.
    private sealed class FakeExtractor(string text = "resume text", Exception? error = null) : IResumeTextExtractor
    {
        public List<ResumeFormat> Formats { get; } = [];
        public List<byte[]> Contents { get; } = [];

        public string ExtractText(byte[] content, ResumeFormat format)
        {
            Formats.Add(format);
            Contents.Add(content);
            return error is null ? text : throw error;
        }
    }

    // Собирает файл: сигнатура и добивка нулями до нужного размера.
    private static byte[] File(byte[] signature, int size = 100)
    {
        var bytes = new byte[Math.Max(size, signature.Length)];
        signature.CopyTo(bytes, 0);
        return bytes;
    }

    [Fact]
    public void Read_EmptyFileThrowsBadRequest()
    {
        var extractor = new FakeExtractor();

        Assert.Throws<BadRequestException>(() => new ResumeReader(extractor).Read([]));

        Assert.Empty(extractor.Formats);
    }

    [Fact]
    public void Read_FileOverTenMegabytesThrowsBadRequest()
    {
        var extractor = new FakeExtractor();
        var content = File(PdfSignature, ResumeReader.MaxFileSize + 1);

        Assert.Throws<BadRequestException>(() => new ResumeReader(extractor).Read(content));

        Assert.Empty(extractor.Formats);
    }

    [Fact]
    public void Read_FileOfExactlyTenMegabytesIsAccepted()
    {
        var extractor = new FakeExtractor();
        var content = File(PdfSignature, 10 * 1024 * 1024);

        var result = new ResumeReader(extractor).Read(content);

        Assert.Equal(ResumeFormat.Pdf, result.Format);
    }

    [Fact]
    public void Read_OldDocFormatThrowsBadRequest()
    {
        var extractor = new FakeExtractor();

        Assert.Throws<BadRequestException>(() => new ResumeReader(extractor).Read(File(OleSignature)));

        Assert.Empty(extractor.Formats);
    }

    [Theory]
    [InlineData(new byte[] { 0x00 })]
    [InlineData(new byte[] { 0x25, 0x50, 0x44 })]
    [InlineData(new byte[] { 0x50, 0x4B, 0x03 })]
    [InlineData(new byte[] { 0x50, 0x4B, 0x05, 0x06, 0x00 })]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 })]
    public void Read_UnknownContentThrowsBadRequest(byte[] content)
    {
        var extractor = new FakeExtractor();

        Assert.Throws<BadRequestException>(() => new ResumeReader(extractor).Read(content));

        Assert.Empty(extractor.Formats);
    }

    [Fact]
    public void Read_TextFileWithoutSignatureThrowsBadRequest() =>
        Assert.Throws<BadRequestException>(() =>
            new ResumeReader(new FakeExtractor()).Read(Encoding.UTF8.GetBytes("просто текст, не резюме")));

    [Fact]
    public void Read_PdfSignatureGivesPdfFormatToExtractor()
    {
        var extractor = new FakeExtractor();
        var content = File(PdfSignature);

        var result = new ResumeReader(extractor).Read(content);

        Assert.Equal(ResumeFormat.Pdf, result.Format);
        Assert.Equal([ResumeFormat.Pdf], extractor.Formats);
        Assert.Same(content, Assert.Single(extractor.Contents));
    }

    [Fact]
    public void Read_ZipSignatureGivesDocxFormatToExtractor()
    {
        var extractor = new FakeExtractor();

        var result = new ResumeReader(extractor).Read(File(ZipSignature));

        Assert.Equal(ResumeFormat.Docx, result.Format);
        Assert.Equal([ResumeFormat.Docx], extractor.Formats);
    }

    [Fact]
    public void Read_ReturnsTextFromExtractor()
    {
        var result = new ResumeReader(new FakeExtractor("Python, SQL")).Read(File(PdfSignature));

        Assert.Equal("Python, SQL", result.Text);
    }

    [Fact]
    public void Read_ExtractorFailureBecomesBadRequest()
    {
        var reader = new ResumeReader(new FakeExtractor(error: new InvalidDataException("broken")));

        var exception = Assert.Throws<BadRequestException>(() => reader.Read(File(PdfSignature)));

        // Детали внутренней ошибки не должны уходить наружу.
        Assert.DoesNotContain("broken", exception.Message);
    }

    [Fact]
    public void Read_AnyExtractorExceptionTypeBecomesBadRequest()
    {
        var reader = new ResumeReader(new FakeExtractor(error: new NullReferenceException()));

        Assert.Throws<BadRequestException>(() => reader.Read(File(ZipSignature)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\t \r\n")]
    public void Read_BlankTextThrowsBadRequest(string text)
    {
        var reader = new ResumeReader(new FakeExtractor(text));

        Assert.Throws<BadRequestException>(() => reader.Read(File(PdfSignature)));
    }

    [Fact]
    public void Read_HashIsLowercaseSha256Of64Characters()
    {
        var content = File(PdfSignature, 500);

        var result = new ResumeReader(new FakeExtractor()).Read(content);

        Assert.Equal(64, result.Hash.Length);
        Assert.Equal(result.Hash.ToLowerInvariant(), result.Hash);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(), result.Hash);
    }

    [Fact]
    public void Read_HashMatchesKnownSha256()
    {
        // SHA-256 от четырёх байт «%PDF» (эталон посчитан внешней утилитой shasum).
        var result = new ResumeReader(new FakeExtractor()).Read(PdfSignature);

        Assert.Equal("315d429b7714cedb6ad04ac31240145257692630457f3c88253c5beceac76027", result.Hash);
    }

    [Fact]
    public void Read_DifferentContentGivesDifferentHash()
    {
        var reader = new ResumeReader(new FakeExtractor());

        var first = reader.Read(File(PdfSignature, 100));
        var second = reader.Read(File(PdfSignature, 101));

        Assert.NotEqual(first.Hash, second.Hash);
    }
}
