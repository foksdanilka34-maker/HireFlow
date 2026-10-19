namespace HireFlow.Application.Recruiters;

public interface IRecruiterService
{
    // Проверяет логин и пароль; неверная пара — null.
    Task<RecruiterResponse?> LoginAsync(LoginRequest request, CancellationToken ct);

    // Создаёт рекрутера или меняет пароль существующему (для консольной команды).
    Task<RecruiterResponse> SetPasswordAsync(string login, string password, CancellationToken ct);
}
