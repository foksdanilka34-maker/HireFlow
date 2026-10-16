using System.Security.Cryptography;
using HireFlow.Application.Common;

namespace HireFlow.Application.Resumes;

public class ResumeReader(IResumeTextExtractor extractor) : IResumeReader
{
    public const int MaxFileSize = 10 * 1024 * 1024;

    public ResumeContent Read(byte[] content)
    {
        if (content.Length == 0)
            throw new BadRequestException("The file is empty.");
        if (content.Length > MaxFileSize)
            throw new BadRequestException("The file must not exceed 10 MB.");

        var format = DetectFormat(content)
            ?? throw new BadRequestException("Only PDF and DOCX files are supported.");

        string text;
        try
        {
            text = extractor.ExtractText(content, format);
        }
        catch (Exception)
        {
            throw new BadRequestException("Could not read the file. Please send another PDF or DOCX.");
        }

        if (string.IsNullOrWhiteSpace(text))
            throw new BadRequestException("The file contains no text (a scan?). Please send a PDF with text or DOCX.");

        var hash = Convert.ToHexStringLower(SHA256.HashData(content));
        return new ResumeContent(format, text, hash);
    }

    // Определяет формат по первым байтам файла: PDF начинается с «%PDF», DOCX — zip-архив («PK»).
    private static ResumeFormat? DetectFormat(byte[] content) => content switch
    {
        [0x25, 0x50, 0x44, 0x46, ..] => ResumeFormat.Pdf,
        [0x50, 0x4B, 0x03, 0x04, ..] => ResumeFormat.Docx,
        _ => null,
    };
}
