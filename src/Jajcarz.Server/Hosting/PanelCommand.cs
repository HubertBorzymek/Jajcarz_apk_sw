using Jajcarz.Application.Panels;

namespace Jajcarz.Server.Hosting;

/// <summary>Rejestracja panelu: <c>panel set device_id</c>. Token pojawia się tylko raz i trafia do secrets.h.</summary>
public static class PanelCommand
{
    private const string Command = "panel";
    private const string SetAction = "set";
    private const int ArgumentCount = 3;

    public static bool Matches(string[] args) => args.Length > 0 && args[0] == Command;

    public static async Task<int> RunAsync(IServiceProvider services, string[] args)
    {
        if (args.Length != ArgumentCount || args[1] != SetAction)
        {
            await Console.Error.WriteLineAsync($"Użycie: {Command} {SetAction} <device_id>");
            return 1;
        }

        await using var scope = services.CreateAsyncScope();
        var panels = scope.ServiceProvider.GetRequiredService<PanelAdminService>();
        try
        {
            Console.WriteLine(await panels.SetTokenAsync(args[2], CancellationToken.None));
        }
        catch (ArgumentException exception)
        {
            await Console.Error.WriteLineAsync(exception.Message);
            return 1;
        }
        return 0;
    }
}
