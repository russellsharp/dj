using System.Text.Json.Serialization;
using shared;
using shared.http;

// var key = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
// File.WriteAllText("./super_secret_key.secret", key);

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (sender, e) =>
{
    Console.WriteLine("\nStopping program...");
    e.Cancel = true; // Prevents the app from closing immediately
    cts.Cancel();    // Sends the cancellation signal
};

var builder = WebApplication.CreateBuilder(args);

//TODO: Add security in JWT Bearer form
//TODO: Add documentation and publishing of endpoints/models
// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.AddConfiguration()
        .AddServices()
        .AddSecurity()
        .AddRateLimiter();

builder.Services.AddSingleton(cts);

var app = builder.Build();
app.SetupSecurity(); //must come before MapControllers
app.MapControllers();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

//initialize the media service to preload the files from database
var media = app.Services.GetRequiredService<IMediaCollection>();
await media.Initialize(cts.Token);

app.Run();