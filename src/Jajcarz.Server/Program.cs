using Jajcarz.Application;
using Jajcarz.Infrastructure;
using Jajcarz.Server.Hosting;
using Jajcarz.Server.Transport;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddJajcarzApplication();
builder.Services.AddJajcarzInfrastructure(builder.Configuration);

var app = builder.Build();

await app.Services.InitializeDatabaseAsync();
if (app.Environment.IsDevelopment())
{
    await DevelopmentSeed.ApplyAsync(app.Services, app.Configuration);
}

app.UseWebSockets();
app.MapPanelSocket();

await app.RunAsync();

/// <summary>Widoczny dla WebApplicationFactory w testach.</summary>
public partial class Program;
