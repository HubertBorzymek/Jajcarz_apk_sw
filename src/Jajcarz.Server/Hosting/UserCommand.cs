using Jajcarz.Application.Accounts;

namespace Jajcarz.Server.Hosting;

/// <summary>Zakładanie kont bez rejestracji: <c>user set login</c>, hasło ze standardowego wejścia.</summary>
public static class UserCommand
{
    private const string Command = "user";
    private const string SetAction = "set";
    private const int ArgumentCount = 3;

    public static bool Matches(string[] args) => args.Length > 0 && args[0] == Command;

    public static async Task<int> RunAsync(IServiceProvider services, string[] args)
    {
        if (args.Length != ArgumentCount || args[1] != SetAction)
        {
            await Console.Error.WriteLineAsync($"Użycie: {Command} {SetAction} <login>, a hasło na standardowym wejściu.");
            return 1;
        }

        var password = await Console.In.ReadLineAsync() ?? "";
        await using var scope = services.CreateAsyncScope();
        var accounts = scope.ServiceProvider.GetRequiredService<AccountService>();
        try
        {
            await accounts.SetPasswordAsync(args[2], password, CancellationToken.None);
        }
        catch (ArgumentException exception)
        {
            await Console.Error.WriteLineAsync(exception.Message);
            return 1;
        }
        Console.WriteLine($"Zapisano konto {AccountService.NormalizeLogin(args[2])}.");
        return 0;
    }
}
