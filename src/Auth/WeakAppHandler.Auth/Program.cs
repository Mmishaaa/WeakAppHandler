using Microsoft.EntityFrameworkCore;
using Npgsql;
using WeakAppHandler.Auth;
using WeakAppHandler.Auth.Persistence;
using WeakAppHandler.Auth.Security;
using WeakAppHandler.ServiceDefaults;
using WeakAppHandler.ServiceDefaults.Cors;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddServiceCors();
builder.Services.AddAuthPersistence(builder.Configuration);
builder.Services.AddOptions<AuthTokenOptions>().Bind(builder.Configuration.GetSection(AuthTokenOptions.SectionName));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<SigningKeyProvider>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<RefreshTokenService>();
builder.Services.AddScoped<ServiceClientTokenService>();
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

    // TASK-047: applied here rather than out-of-band, so a fresh `docker compose up` reaches a
    // working, seeded database (users/service_clients ship as migration HasData) with no manual
    // `dotnet ef database update` step. Migrate() is idempotent, so this is also safe to run
    // against an already-migrated database.
    //
    // Retried rather than a bare call: at container boot, Postgres's own health-gated depends_on
    // only guarantees Postgres itself is ready - not that this container's network attachment or
    // DNS resolution for "postgres" has settled yet. Without a retry, that transient failure is an
    // unhandled exception that crashes the process before Docker Compose finishes attaching every
    // declared network, which can leave the container permanently stuck restart-looping (found via
    // a real docker compose run).
    for (var attempt = 1; ; attempt++)
    {
        try
        {
            await db.Database.MigrateAsync();
            break;
        }
        catch (NpgsqlException) when (attempt < 10)
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }

    var signingKeyProvider = scope.ServiceProvider.GetRequiredService<SigningKeyProvider>();
    var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();
    await SigningKeyInitializer.EnsureInitializedAsync(db, signingKeyProvider, timeProvider, CancellationToken.None);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors(ServiceCorsExtensions.PolicyName);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapServiceDefaultsEndpoints();

app.Run();

public partial class Program;
