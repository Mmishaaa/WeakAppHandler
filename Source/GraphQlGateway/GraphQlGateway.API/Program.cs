using GraphQlGateway.API.Extensions;
using Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.AddApi();

var app = builder.Build();

app.UseGlobalExceptionHandling();

app.MapGraphQL();

app.RunWithGraphQLCommands(args);
