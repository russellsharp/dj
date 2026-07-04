using System.Text.Json.Serialization;
using shared;

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (sender, e) =>
{
    Console.WriteLine("\nStopping program...");
    e.Cancel = true; // Prevents the app from closing immediately
    cts.Cancel();    // Sends the cancellation signal
};

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.AddConfiguration()
        .AddServices();

builder.Services.AddSingleton(cts);

var app = builder.Build();
app.MapControllers();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
app.UseHttpsRedirection();

app.MapGet("/health", () =>
{
    return "yeah";
})
.WithName("health");

var media = app.Services.GetRequiredService<IMediaCollection>();
await media.Initialize(cts.Token);

app.Run();