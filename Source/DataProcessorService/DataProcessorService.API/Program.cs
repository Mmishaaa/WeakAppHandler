using DataProcessorService.API.Endpoints;
using DataProcessorService.API.Extensions;
using DataProcessorService.DAL;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;
using Shared.Configuration;
using Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddDockerSecrets();
builder.Services.AddApi(builder.Configuration);

var app = builder.Build();

app.UseGlobalExceptionHandling();
app.UseSerilogRequestLogging();

app.MapPrometheusScrapingEndpoint();
app.MapHealthChecks("/health");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapApi();

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<ProcessorDbContext>().Database.MigrateAsync();
}

await app.RunAsync();
