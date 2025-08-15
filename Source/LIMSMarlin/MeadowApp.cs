using Meadow;
using Meadow.Hardware;

namespace LIMSMarlin;

public class MeadowApp : App<RaspberryPi>
{
    private IDigitalOutputPort? _fanControl;
    private SensorService _sensorService = default!;
    private PublisherService _publisherService = default!;
    private WebApplication? _webApp = default!;

    public override Task Initialize()
    {
        Resolver.Log.Info("Initialize...");

        try
        {
            _fanControl = Device.Pins.GPIO16.CreateDigitalOutputPort(true);
        }
        catch (Exception ex)
        {
            Resolver.Log.Error($"Failed to initialize GPIO16 for fan control: {ex.Message}");
            _fanControl = null;
        }

        var builder = WebApplication.CreateBuilder();
        _webApp = builder.Build();
        _webApp.UseWebSockets();

        _sensorService = new SensorService(Device);
        _sensorService.DataReady += (s, e) =>
        {
            Resolver.Log.Info($"Sensor data ready: {e}");
            // Publish sensor data to all connected WebSocket clients
            _ = _publisherService.Send(e);
        };
        _publisherService = new PublisherService();

        _webApp.Map("/ws", async (HttpContext context) =>
        {
            await _publisherService.HandleWebSocketAsync(context);
        });

        return base.Initialize();
    }

    public override async Task Run()
    {
        Resolver.Log.Info("Run...");


        var t = _sensorService.Start();

        var port = 5000;
        var urls = $"http://0.0.0.0:{port}";

        Console.WriteLine($"WebSocket server starting on {urls}");
        Console.WriteLine($"WebSocket endpoint: ws://[your-ip]:{port}/ws");

        await _webApp!.RunAsync(urls);
    }
}