using WeakAppHandler.Notification.Api.Alerting;
using WeakAppHandler.Notification.Api.Persistence;
using WeakAppHandler.ServiceDefaults;
using WeakAppHandler.ServiceDefaults.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddNotificationPersistence(builder.Configuration);
builder.Services.AddAlerting();
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Bound to the queue the Processor's readings.stored routing key already targets. Alert events go
// no further than this process: the SignalR hub that consumes them (TASK-031) lives here, so there
// is no second exchange to publish them back onto.
builder.AddServiceMassTransit(
    bus => bus.AddConsumer<ReadingStoredConsumer>(),
    (context, rabbitMq) => rabbitMq.AddReadingsReceiveEndpoint<ReadingStoredConsumer>(
        context, ReadingsTopology.StoredQueueName, ReadingsTopology.StoredRoutingKey));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapServiceDefaultsEndpoints();

app.Run();

public partial class Program;
