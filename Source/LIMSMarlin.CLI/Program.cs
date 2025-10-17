using LIMSMarlin.SDK;
using System.Collections.Concurrent;

internal class Program
{
    private static Client? _client;
    private static readonly object _lockObject = new();
    private static SensorData? _latestData;
    private static readonly ConcurrentQueue<double> _accelerationData = new();
    private static readonly int MaxSamples = 80;
    private static bool _running = true;

    // Persistent last known values
    private static double? _lastTemperature;
    private static double? _lastHumidity;
    private static double? _lastTVOC;
    private static double? _lastCO2;
    private static double? _lastADC0Value;

    // Acceleration axis selection and baseline
    private static int _currentAxis = 2; // 0=X, 1=Y, 2=Z (default to Z)
    private static readonly string[] _axisNames = { "X", "Y", "Z" };
    private static double _baselineX = 0;
    private static double _baselineY = 0;
    private static double _baselineZ = 0;
    private static bool _baselineCollected = false;
    private static double _lastADC1Value;
    private static double _lastADC2Value;
    private static double _lastADC3Value;
    private static readonly List<double> _baselineXSamples = new();
    private static readonly List<double> _baselineYSamples = new();
    private static readonly List<double> _baselineZSamples = new();

    private static async Task Main(string[] args)
    {
        Console.CursorVisible = false;
        Console.Clear();

        string host = "192.168.68.72";
        if (args.Length > 0)
        {
            host = args[0];
        }

        host = "192.168.68.72";
        _client = new Client(host);
        _client.SensorDataReceived += OnSensorDataReceived;

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            _running = false;
        };

        try
        {
            Console.WriteLine($"Connecting to LIMSMarlin at {_client.Host}:{_client.Port}...");
            await _client.ConnectAsync();
            Console.WriteLine("Connected!");

            // Collect baseline for 1 second
            Console.WriteLine("Collecting baseline (1 second)...");
            await Task.Delay(1000);

            lock (_lockObject)
            {
                if (_baselineXSamples.Count > 0)
                {
                    _baselineX = _baselineXSamples.Average();
                    _baselineY = _baselineYSamples.Average();
                    _baselineZ = _baselineZSamples.Average();
                    _baselineCollected = true;
                    Console.WriteLine($"Baseline collected - X: {_baselineX:F3}g, Y: {_baselineY:F3}g, Z: {_baselineZ:F3}g");
                }
                else
                {
                    Console.WriteLine("No baseline data received. Using zero baseline.");
                    _baselineCollected = true;
                }
            }

            await Task.Delay(1000);
            Console.Clear();
            Console.WriteLine("Use ← → arrows to change axis. Press Ctrl+C to exit.");
            await Task.Delay(1000);
            Console.Clear();

            var uiTask = Task.Run(UpdateUI);
            var keyboardTask = Task.Run(HandleKeyboard);

            while (_running)
            {
                if (!_client.IsConnected)
                {
                    Console.WriteLine("Connection lost. Attempting to reconnect...");
                    try
                    {
                        await _client.ConnectAsync();
                    }
                    catch
                    {
                        await Task.Delay(2000);
                    }
                }
                await Task.Delay(100);
            }

            await uiTask;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        finally
        {
            if (_client != null)
            {
                await _client.DisconnectAsync();
                _client.Dispose();
            }
            Console.CursorVisible = true;
        }
    }

    private static void OnSensorDataReceived(object? sender, SensorData data)
    {
        lock (_lockObject)
        {
            _latestData = data;

            // Update persistent values when new data arrives
            if (data.Temperature.HasValue) _lastTemperature = data.Temperature.Value;
            if (data.Humidity.HasValue) _lastHumidity = data.Humidity.Value;
            if (data.TVOC.HasValue) _lastTVOC = data.TVOC.Value;
            if (data.CO2.HasValue) _lastCO2 = data.CO2.Value;
            if (data.ADC0Value.HasValue) _lastADC0Value = data.ADC0Value.Value;
            if (data.ADC1Value.HasValue) _lastADC1Value = data.ADC1Value.Value;
            if (data.ADC2Value.HasValue) _lastADC2Value = data.ADC2Value.Value;
            if (data.ADC3Value.HasValue) _lastADC3Value = data.ADC3Value.Value;

            if (data.AccelerationX.HasValue && data.AccelerationY.HasValue && data.AccelerationZ.HasValue)
            {
                // Collect baseline samples
                if (!_baselineCollected)
                {
                    _baselineXSamples.Add(data.AccelerationX.Value);
                    _baselineYSamples.Add(data.AccelerationY.Value);
                    _baselineZSamples.Add(data.AccelerationZ.Value);
                }
                else
                {
                    // Calculate deviation from baseline for current axis
                    double currentValue = _currentAxis switch
                    {
                        0 => data.AccelerationX.Value - _baselineX,
                        1 => data.AccelerationY.Value - _baselineY,
                        2 => data.AccelerationZ.Value - _baselineZ,
                        _ => 0
                    };

                    _accelerationData.Enqueue(currentValue);

                    while (_accelerationData.Count > MaxSamples)
                    {
                        _accelerationData.TryDequeue(out _);
                    }
                }
            }
        }
    }

    private static async Task UpdateUI()
    {
        while (_running)
        {
            lock (_lockObject)
            {
                Console.SetCursorPosition(0, 0);
                DrawHeader();
                DrawAccelerationChart();
            }

            await Task.Delay(50); // 20 FPS
        }
    }

    private static async Task HandleKeyboard()
    {
        while (_running)
        {
            var keyInfo = Console.ReadKey(true);

            lock (_lockObject)
            {
                switch (keyInfo.Key)
                {
                    case ConsoleKey.LeftArrow:
                        _currentAxis = (_currentAxis - 1 + 3) % 3;
                        _accelerationData.Clear(); // Clear old data when switching axes
                        break;
                    case ConsoleKey.RightArrow:
                        _currentAxis = (_currentAxis + 1) % 3;
                        _accelerationData.Clear(); // Clear old data when switching axes
                        break;
                }
            }

            await Task.Delay(10);
        }
    }

    private static void DrawHeader()
    {
        var width = Console.WindowWidth;

        Console.WriteLine("┌" + new string('─', width - 2) + "┐");
        Console.WriteLine("│" + " LIMSMarlin Sensor Monitor".PadRight(width - 2) + "│");
        Console.WriteLine("├" + new string('─', width - 2) + "┤");

        var temp = _lastTemperature?.ToString("F1") ?? "N/A";
        var humidity = _lastHumidity?.ToString("F1") ?? "N/A";
        var tvoc = _lastTVOC?.ToString("F1") ?? "N/A";
        var co2 = _lastCO2?.ToString("F1") ?? "N/A";
        var adc = $"[{_lastADC0Value:F3},{_lastADC1Value:F3},{_lastADC2Value:F3},{_lastADC3Value:F3}]";

        Console.WriteLine($"│ Temperature: {temp}°C  Humidity: {humidity}%  TVOC: {tvoc}ppm  CO2: {co2}ppm  ADC: {adc}V".PadRight(width - 1) + "│");

        Console.WriteLine("├" + new string('─', width - 2) + "┤");
        Console.WriteLine($"│ Acceleration {_axisNames[_currentAxis]}-axis Deviation (g) - Last {MaxSamples} samples".PadRight(width - 1) + "│");
        Console.WriteLine("└" + new string('─', width - 2) + "┘");
    }

    private static void DrawAccelerationChart()
    {
        var width = Console.WindowWidth - 4;
        var height = 15;

        var deviations = _accelerationData.ToArray();
        if (deviations.Length == 0)
        {
            Console.WriteLine($"  Waiting for {_axisNames[_currentAxis]}-axis data...");
            return;
        }

        var maxDev = deviations.Max();
        var minDev = deviations.Min();
        var range = Math.Max(Math.Abs(maxDev), Math.Abs(minDev));
        if (range == 0) range = 0.1;

        var columns = Math.Min(width, deviations.Length);

        // Draw chart with zero line in the middle
        for (int row = height - 1; row >= 0; row--)
        {
            Console.Write("  ");
            var value = range * (2.0 * row / (height - 1) - 1.0); // -range to +range

            for (int col = 0; col < columns; col++)
            {
                var sampleIndex = (deviations.Length - columns) + col;
                if (sampleIndex >= 0 && sampleIndex < deviations.Length)
                {
                    var deviation = deviations[sampleIndex];

                    // Show zero line
                    if (Math.Abs(value) < range * 0.05)
                    {
                        Console.Write("─");
                    }
                    // Show positive deviation
                    else if (deviation >= 0 && value <= deviation && value >= 0)
                    {
                        Console.Write("█");
                    }
                    // Show negative deviation
                    else if (deviation < 0 && value >= deviation && value <= 0)
                    {
                        Console.Write("█");
                    }
                    else
                    {
                        Console.Write(" ");
                    }
                }
                else
                {
                    Console.Write(" ");
                }
            }
            Console.WriteLine();
        }

        Console.Write("  ");
        Console.WriteLine(new string('─', columns));

        var scaleText = $"Scale: -{range:F3}g to +{range:F3}g";
        Console.WriteLine($"  {scaleText}");

        if (_latestData?.AccelerationX.HasValue == true &&
            _latestData.AccelerationY.HasValue &&
            _latestData.AccelerationZ.HasValue && _baselineCollected)
        {
            var currentDevX = _latestData.AccelerationX.Value - _baselineX;
            var currentDevY = _latestData.AccelerationY.Value - _baselineY;
            var currentDevZ = _latestData.AccelerationZ.Value - _baselineZ;
            var currentSelected = _currentAxis switch
            {
                0 => currentDevX,
                1 => currentDevY,
                2 => currentDevZ,
                _ => 0
            };

            Console.WriteLine($"  Current Deviations: X={currentDevX:F3}g Y={currentDevY:F3}g Z={currentDevZ:F3}g | [{_axisNames[_currentAxis]}={currentSelected:F3}g]");
        }
    }
}