namespace LIMSMarlin;

internal class MainController
{
    private readonly SensorService _sensorService;
    private readonly PublisherService _publisherService;
    private Task? _sensorTask;

    public MainController(SensorService sensorService, PublisherService publisherService)
    {
        _sensorService = sensorService;
        _publisherService = publisherService;
        _sensorService.DataReady += async (s, e) =>
        {
            // Publish sensor data to all connected WebSocket clients
            await publisherService.Send(e);
        };
    }

    public void Start()
    {
        _sensorTask = _sensorService.Start();
    }

    public async Task Stop()
    {
        Meadow.Resolver.Log.Info("Stopping MainController...");

        // Stop sensor service
        _sensorService.Stop();

        // Wait for sensor task to complete (with timeout)
        if (_sensorTask != null)
        {
            await Task.WhenAny(_sensorTask, Task.Delay(5000));
        }

        // Disconnect all WebSocket clients
        await _publisherService.DisconnectAllClients();

        Meadow.Resolver.Log.Info("MainController stopped");
    }
}
