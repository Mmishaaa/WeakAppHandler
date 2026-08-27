using WeakAppHandler.Processor.Infrastructure;
using WeakAppHandler.Processor.Worker;
using WeakAppHandler.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProcessorInfrastructure(builder.Configuration);
builder.Services.AddHostedService<Worker>();

var app = builder.Build();

app.MapServiceDefaultsEndpoints();

app.Run();
