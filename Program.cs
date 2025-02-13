using Microsoft.EntityFrameworkCore;
using Acceloka.Services;
using Acceloka.Entities;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

// Konfigurasi logging dengan Serilog dari appsettings.json
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(configuration)
    .CreateLogger();

// Gunakan Serilog sebagai logger utama sebelum membangun aplikasi
builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Konfigurasi SQL Server
builder.Services.AddEntityFrameworkSqlServer();
builder.Services.AddDbContextPool<AccelokaContext>(options =>
{
    var conString = configuration.GetConnectionString("SQLServerDB");
    options.UseSqlServer(conString);
});

builder.Services.AddTransient<TicketService>();

var app = builder.Build();

// Konfigurasi middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Logging request dengan Serilog
app.UseSerilogRequestLogging();

app.UseAuthorization();
app.MapControllers();

// Tangani log saat aplikasi shutdown
try
{
    Log.Information("Starting the application...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
