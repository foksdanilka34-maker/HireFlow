using HireFlow.Web.Api;

namespace HireFlow.Web.Auth;

// Текущий рекрутер во вкладке браузера; пароль здесь не хранится.
public class AuthState
{
    public RecruiterDto? Recruiter { get; private set; }

    public bool IsSignedIn => Recruiter is not null;

    public event Action? Changed;

    // Запоминает вошедшего рекрутера.
    public void SignIn(RecruiterDto recruiter)
    {
        Recruiter = recruiter;
        Changed?.Invoke();
    }

    // Сбрасывает сессию.
    public void SignOut()
    {
        Recruiter = null;
        Changed?.Invoke();
    }
}
