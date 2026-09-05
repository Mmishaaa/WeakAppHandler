using Microsoft.EntityFrameworkCore;
using Npgsql;
using WeakAppHandler.Processor.Infrastructure;
using WeakAppHandler.Processor.Infrastructure.Ingestion;
using WeakAppHandler.Processor.Infrastructure.Persistence;
using WeakAppHandler.Processor.Worker.Retention;
using WeakAppHandler.ServiceDefaults;
using WeakAppHandler.ServiceDefaults.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProcessorInfrastructure(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddHostedService<RetentionBackgroundService>();

builder.AddServiceMassTransit(
    bus =>
    {
        bus.AddConsumer<ReadingsIngestedConsumer>();
        bus.AddConsumer<IngestAttemptRecordedConsumer>();

        // Not bound to an explicit receive endpoint below: MassTransit publishes Fault<T> to its own
        // convention exchange, independent of the routing keys the ingestion messages themselves use,
        // so ConfigureEndpoints gives each of these its own convention-named queue.
        bus.AddConsumer<ReadingsIngestedFaultConsumer>();
        bus.AddConsumer<IngestAttemptRecordedFaultConsumer>();
    },
    (context, rabbitMq) =>
    {
        // Each consumer gets the queue the Ingestor's routing key already targets, so the two kinds
        // of ingestion message are consumed independently: a poll that failed still records its
        // outcome while the readings queue is backed up, and vice versa.
        rabbitMq.AddReadingsReceiveEndpoint<ReadingsIngestedConsumer>(
            context, ReadingsTopology.IngestedQueueName, ReadingsTopology.IngestedRoutingKey);
        rabbitMq.AddReadingsReceiveEndpoint<IngestAttemptRecordedConsumer>(
            context, ReadingsTopology.AttemptQueueName, ReadingsTopology.AttemptRoutingKey);
    });

var app = builder.Build();

// TASK-047: applied here rather than out-of-band, so a fresh `docker compose up` reaches a
// working, seeded database (metrics ship as migration HasData) with no manual
// `dotnet ef database update` step. MigrateAsync() is idempotent, so this is also safe to run
// against an already-migrated database.
//
// Retried rather than a bare call: see WeakAppHandler.Auth's Program.cs for why a transient
// connection failure at container boot needs a retry here instead of crashing the process.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            await db.Database.MigrateAsync();
            break;
        }
        catch (NpgsqlException) when (attempt < 10)
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }
}

// No UseHttpsRedirection here, unlike the browser-facing hosts: the admin API lives on the backend
// network only (PRD §10) and its one caller is the Gateway's machine client, so a redirect would
// only turn a working internal call into a second round trip.
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapServiceDefaultsEndpoints();

app.Run();

public partial class Program;
