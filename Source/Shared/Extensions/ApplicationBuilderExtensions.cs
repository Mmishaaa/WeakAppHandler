using Microsoft.AspNetCore.Builder;

namespace Shared.Extensions;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        return app;
    }
}
