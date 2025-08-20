using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace LIMSMarlin.SDK;

public class Client : IDisposable
{
    public string Host { get; private set; }
    public int Port { get; private set; } = 5000;
    
    public event EventHandler<SensorData>? SensorDataReceived;
    
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _receiveTask;
    private readonly JsonSerializerOptions _jsonOptions;

    public bool IsConnected => _webSocket?.State == WebSocketState.Open;

    public Client(string host, int port = 5000)
    {
        Host = host;
        Port = port;
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    public Client(IPAddress ipAddress, int port = 5000)
        : this(ipAddress.ToString(), port)
    {
    }

    public async Task ConnectAsync()
    {
        if (_webSocket?.State == WebSocketState.Open)
            return;

        _cancellationTokenSource = new CancellationTokenSource();
        _webSocket = new ClientWebSocket();
        
        var uri = new Uri($"ws://{Host}:{Port}/ws");
        
        await _webSocket.ConnectAsync(uri, _cancellationTokenSource.Token);
        
        _receiveTask = Task.Run(async () => await ReceiveLoop(_cancellationTokenSource.Token));
    }

    public async Task DisconnectAsync()
    {
        if (_webSocket?.State == WebSocketState.Open)
        {
            await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnect", CancellationToken.None);
        }
        
        _cancellationTokenSource?.Cancel();
        
        if (_receiveTask != null)
        {
            await _receiveTask;
        }
    }

    private async Task ReceiveLoop(CancellationToken cancellationToken)
    {
        var buffer = new byte[4096];
        
        while (!cancellationToken.IsCancellationRequested && _webSocket?.State == WebSocketState.Open)
        {
            try
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    
                    try
                    {
                        var sensorData = JsonSerializer.Deserialize<SensorData>(json, _jsonOptions);
                        if (sensorData != null)
                        {
                            SensorDataReceived?.Invoke(this, sensorData);
                        }
                    }
                    catch (JsonException)
                    {
                        // Ignore invalid JSON - might be other message types
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
            }
            catch (WebSocketException)
            {
                break;
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public void Dispose()
    {
        DisconnectAsync().Wait();
        _webSocket?.Dispose();
        _cancellationTokenSource?.Dispose();
    }
}
