using WeakAppHandler.Processor.Infrastructure;
using WeakAppHandler.Processor.Infrastructure.Ingestion;
using WeakAppHandler.ServiceDefaults;
using WeakAppHandler.ServiceDefaults.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProcessorInfrastructure(builder.Configuration);

builder.AddServiceMassTransit(
    bus =>
    {
        bus.AddConsumer<ReadingsIngestedConsumer>();
        bus.AddConsumer<IngestAttemptRecordedConsumer>();
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

app.MapServiceDefaultsEndpoints();

app.Run();
