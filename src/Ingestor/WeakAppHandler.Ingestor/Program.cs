using WeakAppHandler.Ingestor;
using WeakAppHandler.Ingestor.WeakApp;
using WeakAppHandler.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddWeakAppClient();
builder.Services.AddHostedService<Worker>();

var app = builder.Build();

app.MapServiceDefaultsEndpoints();

app.Run();
