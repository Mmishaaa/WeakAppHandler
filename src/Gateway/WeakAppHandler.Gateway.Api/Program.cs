using WeakAppHandler.Gateway.Api.GraphQL;
using WeakAppHandler.Gateway.Infrastructure;
using WeakAppHandler.ServiceDefaults;
using WeakAppHandler.ServiceDefaults.Messaging;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddGatewayInfrastructure(builder.Configuration);

// TASK-032: a second, independent receive endpoint bound to the same readings.stored routing key
// Notification already consumes (TASK-029) - the topic exchange delivers a copy to each queue, so
// this does not compete with Notification for deliveries. Alert events stay off RabbitMQ entirely
// (Notification's IAlertDispatcher dispatches them in-process to its own SignalR hub, TASK-031) -
// onReadingStored streams readings only.
builder.AddServiceMassTransit(
    bus => bus.AddConsumer<ReadingStoredSubscriptionConsumer>(),
    (context, rabbitMq) => rabbitMq.AddReadingsReceiveEndpoint<ReadingStoredSubscriptionConsumer>(
        context, ReadingStoredSubscriptionConsumer.QueueName, ReadingsTopology.StoredRoutingKey));

// Captured once at startup rather than resolved per request: the environment cannot change for the
// life of the process, and DisableIntrospection's own IServiceProvider overload exists for cases
// that genuinely need it (e.g. a feature flag), which this is not.
var allowIntrospection = builder.Environment.IsDevelopment();

builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>()
    .AddSubscriptionType<Subscription>()
    .AddTypeExtension<MeterResolvers>()
    .AddDataLoader<MeterCurrentValuesDataLoader>()
    .AddFiltering()
    .AddSorting()
    .AddProjections()

    // In-memory only (single-replica limitation, same ADR as the SignalR hub): a subscriber only
    // ever sees events delivered to whichever Gateway process it is connected to, which is fine
    // while the Gateway runs as one replica and not otherwise.
    .AddInMemorySubscriptions()

    // TASK-025 (PRD's GraphQL hardening): a request whose selection nests deeper than this is
    // rejected during validation, before any resolver - including the classic introspection
    // recursion (__type { fields { type { fields { ... } } } }), which is why introspection depth
    // is counted too (skipIntrospectionFields defaults to false) rather than exempted.
    .AddMaxExecutionDepthRule(GraphQLSecurityLimits.MaxExecutionDepth)

    // Introspection is how a client discovers the schema it is about to query against - exactly
    // what should not be handed to whatever can reach this endpoint once it leaves Development.
    .DisableIntrospection(disable: !allowIntrospection)
    .ModifyRequestOptions(o => o.IncludeExceptionDetails = builder.Environment.IsDevelopment());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Required for graphql-ws: HotChocolate's subscription transport rides a WebSocket connection to
// the same /graphql endpoint MapGraphQL() maps for HTTP.
app.UseWebSockets();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGraphQL();
app.MapServiceDefaultsEndpoints();

app.Run();

public partial class Program;
