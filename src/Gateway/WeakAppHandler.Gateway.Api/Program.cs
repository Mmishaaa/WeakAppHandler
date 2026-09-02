using WeakAppHandler.Gateway.Api.GraphQL;
using WeakAppHandler.Gateway.Infrastructure;
using WeakAppHandler.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddGatewayInfrastructure(builder.Configuration);

// Captured once at startup rather than resolved per request: the environment cannot change for the
// life of the process, and DisableIntrospection's own IServiceProvider overload exists for cases
// that genuinely need it (e.g. a feature flag), which this is not.
var allowIntrospection = builder.Environment.IsDevelopment();

builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>()
    .AddTypeExtension<MeterResolvers>()
    .AddDataLoader<MeterCurrentValuesDataLoader>()
    .AddFiltering()
    .AddSorting()
    .AddProjections()

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

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGraphQL();
app.MapServiceDefaultsEndpoints();

app.Run();

public partial class Program;
