namespace HireFlow.Application.Recruiters;

public interface IPasswordHasher
{
    // Считает хэш пароля для хранения.
    string Hash(string password);

    // Проверяет пароль по сохранённому хэшу.
    bool Verify(string hash, string password);
}
