using WeakAppHandler.Processor.Worker;
using WeakAppHandler.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddHostedService<Worker>();

var app = builder.Build();

app.MapServiceDefaultsEndpoints();

app.Run();
