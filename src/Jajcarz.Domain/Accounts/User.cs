namespace Jajcarz.Domain.Accounts;

/// <summary>Konto domownika. Nie ma rejestracji: konta zakłada właściciel poleceniem serwera.</summary>
public sealed class User
{
    public const int MaxLoginLength = 32;

    private User()
    {
        Login = "";
        PasswordHash = "";
    }

    public User(Guid id, string login, string passwordHash, DateTimeOffset now)
    {
        Id = id;
        Login = login;
        PasswordHash = passwordHash;
        SessionStamp = Guid.NewGuid();
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public string Login { get; private set; }

    public string PasswordHash { get; private set; }

    /// <summary>Jest w cookie logowania. Nowa wartość wylogowuje wszystkie urządzenia.</summary>
    public Guid SessionStamp { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public void ChangePassword(string passwordHash)
    {
        PasswordHash = passwordHash;
        SessionStamp = Guid.NewGuid();
    }
}
