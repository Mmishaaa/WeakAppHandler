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

builder.Services.AddControllers();

var app = builder.Build();

// No UseHttpsRedirection here, unlike the browser-facing hosts: the admin API lives on the backend
// network only (PRD §10) and its one caller is the Gateway's machine client, so a redirect would
// only turn a working internal call into a second round trip.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapServiceDefaultsEndpoints();

app.Run();
