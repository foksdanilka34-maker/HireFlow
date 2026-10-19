using System.Text;
using HireFlow.Application.Common;
using HireFlow.Application.Recruiters;
using HireFlow.Domain;
using HireFlow.Domain.Recruiters;
using Microsoft.EntityFrameworkCore;

namespace HireFlow.Api.Recruiters;

public static class RecruiterCommand
{
    // Создаёт рекрутера или меняет ему пароль из консоли; возвращает код выхода.
    public static async Task<int> RunAsync(IServiceProvider services, string login)
    {
        string normalized;
        try
        {
            normalized = Recruiter.NormalizeLogin(login);
        }
        catch (DomainException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        var password = ReadPassword("Пароль: ");
        var repeat = ReadPassword("Повторите пароль: ");
        if (password != repeat)
        {
            Console.Error.WriteLine("Пароли не совпадают.");
            return 1;
        }

        using var scope = services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IRecruiterService>();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        try
        {
            var exists = await db.Recruiters.AnyAsync(r => r.Login == normalized);
            var recruiter = await service.SetPasswordAsync(login, password, CancellationToken.None);
            Console.WriteLine(exists
                ? $"Пароль рекрутера {recruiter.Login} обновлён"
                : $"Рекрутер {recruiter.Login} создан");
            return 0;
        }
        catch (Exception ex) when (ex is DomainException or BadRequestException)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    // Читает пароль без вывода на экран; если ввод перенаправлен — обычной строкой.
    private static string ReadPassword(string prompt)
    {
        Console.Write(prompt);
        if (Console.IsInputRedirected)
            return Console.ReadLine() ?? "";

        var result = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
                break;
            if (key.Key == ConsoleKey.Backspace)
            {
                if (result.Length > 0)
                    result.Length--;
            }
            else if (!char.IsControl(key.KeyChar))
            {
                result.Append(key.KeyChar);
            }
        }

        Console.WriteLine();
        return result.ToString();
    }
}
