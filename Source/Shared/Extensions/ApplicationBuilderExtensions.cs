using Microsoft.AspNetCore.Builder;

namespace Shared.Extensions;

public static class ApplicationBuilderExtensions
{
    extension(IApplicationBuilder app)
    {
        public IApplicationBuilder UseGlobalExceptionHandling()
        {
            app.UseExceptionHandler();
            app.UseStatusCodePages();

            return app;
        }
    }
}
