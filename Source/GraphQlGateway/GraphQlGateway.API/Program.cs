using GraphQlGateway.API.Extensions;
using Serilog;
using Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddApi();

var app = builder.Build();

app.UseGlobalExceptionHandling();
app.UseSerilogRequestLogging();

app.MapPrometheusScrapingEndpoint();
app.MapHealthChecks("/health");
app.MapGraphQL();

app.RunWithGraphQLCommands(args);
