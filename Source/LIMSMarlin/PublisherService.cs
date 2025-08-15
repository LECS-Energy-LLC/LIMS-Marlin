using Meadow;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LIMSMarlin;

internal class PublisherService
{
    private readonly ConcurrentDictionary<string, WebSocket> _connections = new();
    private readonly JsonSerializerOptions options = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public PublisherService()
    {
    }

    /// <summary>
    /// Handles incoming WebSocket connections
    /// </summary>
    public async Task HandleWebSocketAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            return;
        }

        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var connectionId = Guid.NewGuid().ToString();

        _connections.TryAdd(connectionId, webSocket);
        Resolver.Log.Info($"WebSocket connection established: {connectionId}");

        try
        {
            await HandleWebSocketCommunication(webSocket, connectionId);
        }
        catch (Exception ex)
        {
            Resolver.Log.Error(ex, $"Error handling WebSocket connection {connectionId}");
        }
        finally
        {
            _connections.TryRemove(connectionId, out _);
            Resolver.Log.Info($"WebSocket connection closed: {connectionId}");
        }
    }

    /// <summary>
    /// Sends data to all connected WebSocket clients
    /// </summary>
    public async Task Send<T>(T data)
    {
        if (_connections.IsEmpty)
        {
            Resolver.Log.Warn("No WebSocket clients connected");
            return;
        }

        var json = JsonSerializer.Serialize(data, options);
        var buffer = Encoding.UTF8.GetBytes(json);
        var segment = new ArraySegment<byte>(buffer);

        var tasks = new List<Task>();
        var connectionsToRemove = new List<string>();

        foreach (var kvp in _connections)
        {
            var connectionId = kvp.Key;
            var webSocket = kvp.Value;

            if (webSocket.State == WebSocketState.Open)
            {
                tasks.Add(SendToClientAsync(webSocket, segment, connectionId));
            }
            else
            {
                connectionsToRemove.Add(connectionId);
            }
        }

        // Remove closed connections
        foreach (var connectionId in connectionsToRemove)
        {
            _connections.TryRemove(connectionId, out _);
            Resolver.Log.Info($"Removed closed connection: {connectionId}");
        }

        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
            Resolver.Log.Info($"Data sent to {tasks.Count} WebSocket clients");
        }
    }

    /// <summary>
    /// Sends a string message to all connected clients
    /// </summary>
    public async Task SendMessage(string message)
    {
        await Send(new { message, timestamp = DateTime.UtcNow });
    }

    /// <summary>
    /// Gets the count of currently connected clients
    /// </summary>
    public int ConnectedClientsCount => _connections.Count;

    /// <summary>
    /// Closes all WebSocket connections and clears the connection pool
    /// </summary>
    public async Task DisconnectAllClients()
    {
        var tasks = new List<Task>();

        foreach (var kvp in _connections)
        {
            var webSocket = kvp.Value;
            if (webSocket.State == WebSocketState.Open)
            {
                tasks.Add(webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Server shutdown",
                    CancellationToken.None));
            }
        }

        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
        }

        _connections.Clear();
        Resolver.Log.Info("All WebSocket connections closed");
    }

    private async Task SendToClientAsync(WebSocket webSocket, ArraySegment<byte> data, string connectionId)
    {
        try
        {
            await webSocket.SendAsync(
                data,
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            Resolver.Log.Error(ex, $"Failed to send data to client {connectionId}");
        }
    }

    private async Task HandleWebSocketCommunication(WebSocket webSocket, string connectionId)
    {
        var buffer = new byte[4096];

        while (webSocket.State == WebSocketState.Open)
        {
            try
            {
                var result = await webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Resolver.Log.Info($"Received message from {connectionId}: {message}");

                    // Echo the message back or handle as needed
                    // You can add custom message handling logic here
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Closing",
                        CancellationToken.None);
                    break;
                }
            }
            catch (WebSocketException ex)
            {
                Resolver.Log.Warn($"WebSocket exception for connection {connectionId}: {ex.Message}");
                break;
            }
        }
    }
}
