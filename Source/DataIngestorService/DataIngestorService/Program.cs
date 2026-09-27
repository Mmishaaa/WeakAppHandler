using DataIngestorService.Extensions;
using Shared.Configuration;
using Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddDockerSecrets();
builder.Services.AddApi(builder.Configuration);

var app = builder.Build();

app.UseGlobalExceptionHandling();

app.MapPrometheusScrapingEndpoint();
app.MapHealthChecks("/health");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

await app.RunAsync();
