using WeakAppHandler.Auth;
using WeakAppHandler.Auth.Persistence;
using WeakAppHandler.Auth.Security;
using WeakAppHandler.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
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
    var signingKeyProvider = scope.ServiceProvider.GetRequiredService<SigningKeyProvider>();
    var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();
    await SigningKeyInitializer.EnsureInitializedAsync(db, signingKeyProvider, timeProvider, CancellationToken.None);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapServiceDefaultsEndpoints();

app.Run();

public partial class Program;
