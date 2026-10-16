namespace HireFlow.Application.Resumes;

public interface IResumeTextExtractor
{
    // Достаёт текст из файла резюме. Бросает исключение, если файл повреждён.
    string ExtractText(byte[] content, ResumeFormat format);
}
