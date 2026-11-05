using System.Net;
using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using System.Text.Json;
using Jajcarz.Application.Products;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Jajcarz.Integration.Tests.Support;

/// <summary>Klient działający jak przeglądarka: cookie i token CSRF w nagłówku.</summary>
public sealed class ApiClient(TestApp app) : IAsyncDisposable
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    private readonly HttpClient _http = app.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
    private string? _csrfToken;

    public static async Task<ApiClient> LoggedInAsync(TestApp app, string login = TestApp.UserLogin)
    {
        var client = new ApiClient(app);
        var response = await client.LoginAsync(login, TestApp.UserPassword);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        return client;
    }

    public async Task<HttpResponseMessage> LoginAsync(string login, string password)
    {
        await RefreshSessionAsync();
        var response = await SendAsync(HttpMethod.Post, "/api/session", new { login, password });
        await RefreshSessionAsync();
        return response;
    }

    public async Task<string?> RefreshSessionAsync()
    {
        var session = await _http.GetFromJsonAsync<JsonElement>("/api/session");
        _csrfToken = session.GetProperty("csrfToken").GetString();
        return session.GetProperty("login").GetString();
    }

    public Task<HttpResponseMessage> GetAsync(string path) => _http.GetAsync(path);

    public Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body = null, bool withCsrf = true)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }
        if (withCsrf)
        {
            request.Headers.Add("X-CSRF-TOKEN", _csrfToken);
        }
        return _http.SendAsync(request);
    }

    public async Task<ProductView> CreateProductAsync(string name, string? category = null)
    {
        var response = await SendAsync(HttpMethod.Post, "/api/products", new { name, category });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProductView>(Json))!;
    }

    public async Task<ProductStateResult> ChangeStateAsync(Guid id, string state, long ageMs = 0, string? changeKey = null)
    {
        var response = await SendAsync(HttpMethod.Post, $"/api/products/{id}/state", new { state, ageMs, changeKey });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProductStateResult>(Json))!;
    }

    public async Task<IReadOnlyList<ProductView>> ListAsync() =>
        (await _http.GetFromJsonAsync<List<ProductView>>("/api/products", Json))!;

    public async Task<EventStream> OpenEventsAsync()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/events");
        var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return new EventStream(response, await response.Content.ReadAsStreamAsync());
    }

    public ValueTask DisposeAsync()
    {
        _http.Dispose();
        return ValueTask.CompletedTask;
    }

    public sealed record ProductStateResult(bool Accepted, ProductView Product);
}

/// <summary>Otwarty strumień zdarzeń, czytany tak jak robi to EventSource.</summary>
public sealed class EventStream(HttpResponseMessage response, Stream stream) : IAsyncDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly CancellationTokenSource _lifetime = new();
    private readonly IAsyncEnumerator<SseItem<string>> _items =
        SseParser.Create(stream).EnumerateAsync().GetAsyncEnumerator();

    public async Task<(string Type, JsonElement Data)> NextAsync()
    {
        var moved = await _items.MoveNextAsync().AsTask().WaitAsync(Timeout, _lifetime.Token);
        Assert.True(moved, "Strumień zdarzeń się skończył");
        return (_items.Current.EventType, JsonDocument.Parse(_items.Current.Data).RootElement);
    }

    public async Task<JsonElement> ExpectAsync(string type)
    {
        var (actual, data) = await NextAsync();
        Assert.Equal(type, actual);
        return data;
    }

    public async ValueTask DisposeAsync()
    {
        await _lifetime.CancelAsync();
        response.Dispose();
        _lifetime.Dispose();
    }
}
