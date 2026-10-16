namespace HireFlow.Api.Common;

public static class FormFileExtensions
{
    // Читает загруженный файл целиком в массив байтов.
    public static async Task<byte[]> ReadAllBytesAsync(this IFormFile file, CancellationToken ct)
    {
        using var memory = new MemoryStream();
        await file.CopyToAsync(memory, ct);
        return memory.ToArray();
    }
}
