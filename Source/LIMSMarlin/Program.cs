using Meadow;
using Meadow.AspNetCore.Builder;

namespace LIMSMarlin;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton<SensorService>();
        builder.Services.AddSingleton<PublisherService>();
        builder.Services.AddSingleton<MainController>();

        builder.UseMeadow<RaspberryPi>();

        var app = builder.Build();
        app.UseWebSockets();

        var publisher = app.Services.GetService<PublisherService>();
        app.Map("/ws", publisher!.HandleWebSocketAsync);

        var s = app.Services.GetService<MainController>();
        s?.Start();

        var port = 5000;
        var urls = $"http://0.0.0.0:{port}";

        Console.WriteLine($"WebSocket server starting on {urls}");
        Console.WriteLine($"WebSocket endpoint: ws://[0.0.0.0]:{port}/ws");

        await app.RunAsync(urls);
    }
}
