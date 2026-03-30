using Serilog;
using RedditAnalyzer.Services;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("out.log", rollingInterval: RollingInterval.Day, shared: true)
    .CreateLogger();

try
{
    Log.Information("Starting RedditAnalyzer API...");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.Encoder =
            System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping);
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Services.AddHttpClient<IRedditService, RedditService>(client =>
    {
        client.BaseAddress = new Uri("https://www.reddit.com");
        client.DefaultRequestHeaders.Add("User-Agent", "RedditAnalyzer/1.0 (by /u/analyzer_bot)");
        client.Timeout = TimeSpan.FromSeconds(30);
    });

    var app = builder.Build();

    app.UseSwagger();
    app.UseSwaggerUI();

    // Serve static files (UI)
    app.UseDefaultFiles();
    app.UseStaticFiles();

    app.UseAuthorization();
    app.MapControllers();

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
