using DataIngestorService;
using DataIngestorService.Configuration;
using DataIngestorService.Extensions;
using DataIngestorService.Workers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddWeakAppClient(builder.Configuration);
builder.Services.AddHostedService<MeterIngestionWorker>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

await app.RunAsync();
