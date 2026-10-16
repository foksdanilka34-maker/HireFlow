namespace HireFlow.Application.Resumes;

public record ResumeContent(ResumeFormat Format, string Text, string Hash);

public interface IResumeReader
{
    // Проверяет файл резюме и достаёт из него текст. Неподходящий файл — BadRequestException.
    ResumeContent Read(byte[] content);
}
