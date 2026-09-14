using System.Net.WebSockets;
using last.Core.Connection.Models;

namespace last.Core.Connection.WebSocket;

public interface ILcuWebSocketClient : IDisposable, IAsyncDisposable
{
    bool IsConnected { get; }

    event Action? Connected;

    event Action<WebSocketCloseStatus?, string?>? Disconnected;

    event Action<Exception>? ErrorOccurred;

    Task ConnectAsync(LcuCredentials credentials, CancellationToken cancellationToken = default);

    Task DisconnectAsync(CancellationToken cancellationToken = default);

    ValueTask SendAsync(
        ReadOnlyMemory<byte> message,
        WebSocketMessageType messageType = WebSocketMessageType.Text,
        bool endOfMessage = true,
        CancellationToken cancellationToken = default);
}