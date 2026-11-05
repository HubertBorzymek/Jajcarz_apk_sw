using Jajcarz.Application.Abstractions;
using Jajcarz.Domain.Accounts;

namespace Jajcarz.Application.Accounts;

/// <summary>Logowanie i zakładanie kont.</summary>
public sealed class AccountService(IStore store, TimeProvider clock)
{
    public const int MinPasswordLength = 8;

    /// <summary>Weryfikacja trwa tyle samo bez konta, więc czas odpowiedzi nie zdradza loginów.</summary>
    private static readonly string UnknownUserHash = PasswordHasher.Hash(Guid.NewGuid().ToString());

    public static string NormalizeLogin(string login) => login.Trim().ToLowerInvariant();

    public async Task<User?> SignInAsync(string login, string password, CancellationToken cancellationToken)
    {
        var user = await store.FindUserByLoginAsync(NormalizeLogin(login), cancellationToken);
        var valid = PasswordHasher.Verify(password, user?.PasswordHash ?? UnknownUserHash);
        return valid ? user : null;
    }

    public async Task<bool> IsSessionValidAsync(Guid userId, Guid sessionStamp, CancellationToken cancellationToken)
    {
        var user = await store.FindUserAsync(userId, cancellationToken);
        return user?.SessionStamp == sessionStamp;
    }

    public async Task<bool> UserExistsAsync(string login, CancellationToken cancellationToken) =>
        await store.FindUserByLoginAsync(NormalizeLogin(login), cancellationToken) is not null;

    /// <summary>Zakłada konto albo zmienia hasło. Zmiana hasła wylogowuje konto ze wszystkich urządzeń.</summary>
    public async Task SetPasswordAsync(string login, string password, CancellationToken cancellationToken)
    {
        var normalized = NormalizeLogin(login);
        if (normalized.Length is 0 or > User.MaxLoginLength)
        {
            throw new ArgumentException($"Login musi mieć od 1 do {User.MaxLoginLength} znaków.", nameof(login));
        }
        if (password.Length < MinPasswordLength)
        {
            throw new ArgumentException($"Hasło musi mieć co najmniej {MinPasswordLength} znaków.", nameof(password));
        }

        var hash = PasswordHasher.Hash(password);
        await using var write = await store.BeginWriteAsync(cancellationToken);
        var user = await store.FindUserByLoginAsync(normalized, cancellationToken);
        if (user is null)
        {
            store.Add(new User(Guid.NewGuid(), normalized, hash, clock.GetUtcNow()));
        }
        else
        {
            user.ChangePassword(hash);
        }
        await write.CommitAsync(cancellationToken);
    }
}
