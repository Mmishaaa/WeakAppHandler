using NotificationService.API.Extensions;
using NotificationService.API.Hubs;
using Shared.Configuration;
using Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddSharedThresholds();
builder.Services.AddApi(builder.Configuration);

var app = builder.Build();

app.UseGlobalExceptionHandling();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors();

app.MapPrometheusScrapingEndpoint();
app.MapHub<ReadingsHub>("/hubs/readings");

await app.RunAsync();
