using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using HireFlow.Application.Resumes;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace HireFlow.Infrastructure.Resumes;

public class ResumeTextExtractor : IResumeTextExtractor
{
    public string ExtractText(byte[] content, ResumeFormat format) => format switch
    {
        ResumeFormat.Pdf => ExtractFromPdf(content),
        ResumeFormat.Docx => ExtractFromDocx(content),
        _ => throw new ArgumentOutOfRangeException(nameof(format)),
    };

    // Собирает текст всех страниц PDF в порядке чтения.
    private static string ExtractFromPdf(byte[] content)
    {
        using var document = PdfDocument.Open(content);
        var pages = document.GetPages().Select(page => ContentOrderTextExtractor.GetText(page));
        return string.Join("\n", pages);
    }

    // Собирает текст всех абзацев DOCX, каждый с новой строки.
    private static string ExtractFromDocx(byte[] content)
    {
        using var stream = new MemoryStream(content);
        using var document = WordprocessingDocument.Open(stream, isEditable: false);

        var body = document.MainDocumentPart?.Document?.Body
            ?? throw new InvalidDataException("DOCX has no document body.");

        return string.Join("\n", body.Descendants<Paragraph>().Select(p => p.InnerText));
    }
}
