using System.Text.Json.Serialization;
using Jajcarz.Application;
using Jajcarz.Infrastructure;
using Jajcarz.Server.Api;
using Jajcarz.Server.Hosting;
using Jajcarz.Server.Transport;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddJajcarzApplication();
builder.Services.AddJajcarzInfrastructure(builder.Configuration);
builder.Services.AddJajcarzWebSecurity(builder.Configuration);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

await app.Services.InitializeDatabaseAsync();
if (UserCommand.Matches(args))
{
    return await UserCommand.RunAsync(app.Services, args);
}
if (app.Environment.IsDevelopment())
{
    await DevelopmentSeed.ApplyAsync(app.Services, app.Configuration);
}

app.UseForwardedHeaders();
app.UseSecurityHeaders();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseWebSockets();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapPanelSocket();
app.MapSessionEndpoints();
app.MapProductEndpoints();
app.MapEventStream();
app.MapFallback("/api/{**rest}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

await app.RunAsync();
return 0;

/// <summary>Widoczny dla WebApplicationFactory w testach.</summary>
public partial class Program;
