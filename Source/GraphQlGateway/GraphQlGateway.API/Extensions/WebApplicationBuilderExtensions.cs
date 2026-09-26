using GraphQlGateway.API.Diagnostics;
using GraphQlGateway.API.Errors;
using GraphQlGateway.API.Schema;
using GraphQlGateway.BLL.Extensions;
using GraphQlGateway.DAL;
using Shared.Extensions;
using Shared.Logging;
using Shared.Telemetry;

namespace GraphQlGateway.API.Extensions;

internal static class WebApplicationBuilderExtensions
{
    extension(WebApplicationBuilder builder)
    {
        public WebApplicationBuilder AddApi()
        {
            builder.Services.AddSerilogLogging(builder.Configuration, "GraphQlGateway");
            builder.Services.AddTelemetry(builder.Configuration, "GraphQlGateway");
            builder.Services.AddGlobalExceptionHandling();
            builder.Services.AddBll(builder.Configuration);

            var includeExceptionDetails = builder.Environment.IsDevelopment();

            builder.AddGraphQL()
                .AddQueryType<Query>()
                .RegisterDbContextFactory<GatewayDbContext>()
                .AddDbContextCursorPagingProvider(defaultProvider: true)
                .AddFiltering()
                .AddSorting()
                .ModifyRequestOptions(options => options.IncludeExceptionDetails = includeExceptionDetails)
                .AddDiagnosticEventListener(services => new GraphQlDiagnosticEventListener(
                    services.GetRootServiceProvider()
                        .GetRequiredService<ILogger<GraphQlDiagnosticEventListener>>()))
                .AddErrorFilter(error => GraphQlErrors.Sanitize(error, includeExceptionDetails));

            return builder;
        }
    }
}
