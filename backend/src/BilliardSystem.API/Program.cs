using System.Text.Json.Serialization;
using BilliardSystem.API.Endpoints;
using BilliardSystem.API.Hubs;
using BilliardSystem.Infrastructure;
using BilliardSystem.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Pin the SQLite file to the content root so history survives restarts
// regardless of the shell's current directory.
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
builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularDev", policy =>
        policy.WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

var app = builder.Build();

await app.Services.InitializeDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AngularDev");

app.MapBilliardEndpoints();
app.MapHub<TableHub>("/hubs/tables");

app.Run();
