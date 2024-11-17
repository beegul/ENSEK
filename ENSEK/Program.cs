using ENSEK.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ENSEK API", Version = "v1" });
});

// Use an in memory database for the application.
builder.Services.AddDbContext<MeterReadingContext>(options =>
    options.UseInMemoryDatabase("MeterReadingDb"));

// Configure logging.
builder.Services.AddLogging(loggingBuilder =>
{
    loggingBuilder.ClearProviders(); 
    loggingBuilder.AddFilter(level => level >= LogLevel.Information); // Filter logs at Information level or higher.
    loggingBuilder.AddDebug(); 
});

// Configure Serilog for logging.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ENSEK API V1");
    });
}

app.UseHttpsRedirection();
app.UseStaticFiles(); // Allow serving static files (e.g., from wwwroot).
app.UseAuthorization();
app.MapControllers();

// Seed the database with test data.
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<MeterReadingContext>();

    var csvFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Test_Accounts.csv");
    context.SeedData(csvFilePath); 
}

app.Run();