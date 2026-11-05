using System.Net;
using Jajcarz.Application.Accounts;
using Jajcarz.Integration.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Jajcarz.Integration.Tests;

public class SessionTests : IAsyncLifetime
{
    private readonly TestApp _app = new();

    public Task InitializeAsync() => _app.InitializeAsync();

    public Task DisposeAsync() => ((IAsyncLifetime)_app).DisposeAsync();

    [Theory]
    [InlineData("/api/products")]
    [InlineData("/api/events")]
    public async Task PrivateDataNeedsLogin(string path)
    {
        await using var anonymous = new ApiClient(_app);

        var response = await anonymous.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task WrongPasswordIsRejected()
    {
        await using var client = new ApiClient(_app);

        var response = await client.LoginAsync(TestApp.UserLogin, "zle-haslo-123");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(await client.RefreshSessionAsync());
    }

    [Fact]
    public async Task UnknownUserGetsTheSameAnswer()
    {
        await using var client = new ApiClient(_app);

        var response = await client.LoginAsync("ktos-obcy", TestApp.UserPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LoginIgnoresCaseAndSpaces()
    {
        await using var client = new ApiClient(_app);

        var response = await client.LoginAsync(" HUBERT ", TestApp.UserPassword);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(TestApp.UserLogin, await client.RefreshSessionAsync());
    }

    [Fact]
    public async Task SessionCookieLastsAYearAndIsHiddenFromScripts()
    {
        await using var client = new ApiClient(_app);

        var response = await client.LoginAsync(TestApp.UserLogin, TestApp.UserPassword);

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), value => value.StartsWith("jajcarz.auth="));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        var expires = DateTimeOffset.Parse(cookie.Split("expires=")[1].Split(';')[0]);
        Assert.Equal(_app.Clock.Now.AddDays(365).Date, expires.UtcDateTime.Date);
    }

    [Fact]
    public async Task ChangeWithoutCsrfTokenIsRejected()
    {
        await using var client = await ApiClient.LoggedInAsync(_app);

        var response = await client.SendAsync(HttpMethod.Post, "/api/products", new { name = "Mleko" }, withCsrf: false);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(await client.ListAsync());
    }

    [Fact]
    public async Task LogoutEndsSession()
    {
        await using var client = await ApiClient.LoggedInAsync(_app);

        var response = await client.SendAsync(HttpMethod.Delete, "/api/session");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/products")).StatusCode);
    }

    [Fact]
    public async Task PasswordChangeLogsOutOtherDevices()
    {
        await using var phone = await ApiClient.LoggedInAsync(_app);

        await UseAccountsAsync(accounts => accounts.SetPasswordAsync(TestApp.UserLogin, "nowe-haslo-123", CancellationToken.None));

        Assert.Equal(HttpStatusCode.Unauthorized, (await phone.GetAsync("/api/products")).StatusCode);
    }

    [Fact]
    public async Task TooManyLoginAttemptsAreLimited()
    {
        await using var client = new ApiClient(_app);
        await client.RefreshSessionAsync();

        HttpResponseMessage? last = null;
        for (var attempt = 0; attempt < 11; attempt++)
        {
            last = await client.SendAsync(HttpMethod.Post, "/api/session", new { login = "x", password = "y" });
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
    }

    [Fact]
    public async Task PasswordIsNotStoredInPlainText()
    {
        var user = await _app.UseStoreAsync(store => store.FindUserByLoginAsync(TestApp.UserLogin, CancellationToken.None));

        Assert.DoesNotContain(TestApp.UserPassword, user!.PasswordHash);
        Assert.StartsWith("pbkdf2-sha512$", user.PasswordHash);
    }

    [Theory]
    [InlineData("krotkie", "hubert")]
    [InlineData("wystarczajaco-dlugie", "")]
    public async Task AccountRulesAreEnforced(string password, string login)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            UseAccountsAsync(accounts => accounts.SetPasswordAsync(login, password, CancellationToken.None)));
    }

    [Fact]
    public async Task ResponsesCarrySecurityHeaders()
    {
        await using var client = new ApiClient(_app);

        var response = await client.GetAsync("/api/session");

        Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task UnknownApiPathIsNotFound()
    {
        await using var client = new ApiClient(_app);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/nie-ma")).StatusCode);
    }

    private async Task UseAccountsAsync(Func<AccountService, Task> action)
    {
        await using var scope = _app.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<AccountService>());
    }
}
