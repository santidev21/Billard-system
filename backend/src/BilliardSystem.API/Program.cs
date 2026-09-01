using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using BilliardSystem.API.Auth;
using BilliardSystem.API.Endpoints;
using BilliardSystem.API.Hubs;
using BilliardSystem.Infrastructure;
using BilliardSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

var conn = builder.Configuration.GetConnectionString("BilliardDatabase");
if (conn is not null && conn.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
{
    var file = conn["Data Source=".Length..].Trim();
    if (!Path.IsPathRooted(file) && !string.Equals(file, ":memory:", StringComparison.OrdinalIgnoreCase))
    {
        var absolute = Path.Combine(builder.Environment.ContentRootPath, file);
        builder.Configuration["ConnectionStrings:BilliardDatabase"] = $"Data Source={absolute}";
    }
}

builder.Services.AddOpenApi();
builder.Services.AddSignalR();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddAuthentication("AdminSession")
    .AddScheme<AdminAuthOptions, AdminAuthHandler>("AdminSession", _ => { });
builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter("Login", limiter =>
    {
        limiter.PermitLimit = 5;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 0;
    });

    options.AddFixedWindowLimiter("ApiWrite", limiter =>
    {
        limiter.PermitLimit = 60;
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.QueueLimit = 10;
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

await app.Services.InitializeDatabaseAsync();

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseRateLimiter();
app.UseSecurityHeaders();
app.UseAuthentication();
app.UseAuthorization();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapBilliardEndpoints();
app.MapHub<TableHub>("/hubs/tables");

app.MapFallbackToFile("index.html");

app.Run();
