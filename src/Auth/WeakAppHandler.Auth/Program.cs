using Microsoft.EntityFrameworkCore;
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
    await db.Database.MigrateAsync();

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
