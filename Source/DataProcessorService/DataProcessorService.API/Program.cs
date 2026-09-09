using DataProcessorService.API.Extensions;
using DataProcessorService.DAL;
using Microsoft.EntityFrameworkCore;
using Shared.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApi(builder.Configuration);

var app = builder.Build();

app.UseGlobalExceptionHandling();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<ProcessorDbContext>().Database.MigrateAsync();
}

await app.RunAsync();
