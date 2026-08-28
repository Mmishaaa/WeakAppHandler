using WeakAppHandler.Ingestor.Polling;
using WeakAppHandler.Ingestor.WeakApp;
using WeakAppHandler.ServiceDefaults;
using WeakAppHandler.ServiceDefaults.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddWeakAppClient();
builder.AddServiceMassTransit();

// Registered after the bus on purpose: hosted services start in registration order, and the first
// poll happens immediately, so the loop must not run before the bus it publishes through is up.
builder.AddIngestionPolling();

var app = builder.Build();

app.MapServiceDefaultsEndpoints();

app.Run();
