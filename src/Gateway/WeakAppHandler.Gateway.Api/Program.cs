using WeakAppHandler.Gateway.Api.GraphQL;
using WeakAppHandler.Gateway.Infrastructure;
using WeakAppHandler.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddGatewayInfrastructure(builder.Configuration);

builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>()
    .AddTypeExtension<MeterResolvers>()
    .AddDataLoader<MeterCurrentValuesDataLoader>()
    .AddFiltering()
    .AddSorting()
    .AddProjections()
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
