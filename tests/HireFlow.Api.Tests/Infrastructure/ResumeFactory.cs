using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace HireFlow.Api.Tests.Infrastructure;

// Генерирует синтетические резюме в коде: бинарные файлы в репозитории не хранятся.
public static class ResumeFactory
{
    // Собирает DOCX, в котором каждый элемент lines — отдельный абзац.
    public static byte[] Docx(params string[] lines)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            main.Document = new Document(new Body(lines.Select(l => new Paragraph(new Run(new Text(l))))));
            main.Document.Save();
        }

        return stream.ToArray();
    }

    // Собирает PDF с текстом, каждый элемент lines — отдельная строка.
    public static byte[] Pdf(params string[] lines)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(595, 842);

        var y = 800.0;
        foreach (var line in lines)
        {
            page.AddText(line, 12, new UglyToad.PdfPig.Core.PdfPoint(40, y), font);
            y -= 20;
        }

        return builder.Build();
    }

    // Собирает PDF без текста (как скан): страница есть, текста нет.
    public static byte[] EmptyPdf()
    {
        var builder = new PdfDocumentBuilder();
        builder.AddPage(595, 842);
        return builder.Build();
    }

    // Возвращает признак старого .doc (OLE2): такой файл API должен отклонить.
    public static byte[] OldDoc() => [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0, 0, 0, 0];
}
