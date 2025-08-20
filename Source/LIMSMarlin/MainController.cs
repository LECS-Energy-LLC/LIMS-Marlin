namespace LIMSMarlin;

internal class MainController
{
    private readonly SensorService _sensorService;
    private Task? _sensorTask;

    public MainController(SensorService sensorService, PublisherService publisherService)
    {
        _sensorService = sensorService;
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
}
