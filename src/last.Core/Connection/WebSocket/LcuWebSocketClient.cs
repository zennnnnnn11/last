using System.Buffers;
using System.Net.WebSockets;
using System.Threading.Channels;
using last.Core.Connection.Events;
using last.Core.Connection.Models;

namespace last.Core.Connection.WebSocket;

public sealed class LcuWebSocketClient : ILcuWebSocketClient
{
    private const int BufferSize = 16 * 1024;
    private static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromMilliseconds(17500);
    private readonly TimeSpan _connectTimeout;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    private readonly ILcuEventBus _eventBus;
    private CancellationTokenSource? _cts;
    private bool _disposed;
    private Channel<byte[]>? _frameChannel;
    private Task? _processTask;
    private Task? _receiveTask;

    private ClientWebSocket? _webSocket;

    public LcuWebSocketClient(ILcuEventBus eventBus, TimeSpan? connectTimeout = null)
    {
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        _connectTimeout = connectTimeout ?? DefaultConnectTimeout;
    }

    public bool IsConnected => _webSocket is { State: WebSocketState.Open };

    public event Action? Connected;

    public event Action<WebSocketCloseStatus?, string?>? Disconnected;

    public event Action<Exception>? ErrorOccurred;

    public async Task ConnectAsync(LcuCredentials credentials, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsConnected)
                await DisconnectCoreAsync(cancellationToken).ConfigureAwait(false);

            var ws = new ClientWebSocket();
            ws.Options.RemoteCertificateValidationCallback = static (_, _, _, _) => true;
            ws.Options.SetRequestHeader("Authorization", $"Basic {credentials.BasicAuthHeaderValue}");

            using var timeoutCts = new CancellationTokenSource(_connectTimeout);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var endpointUri = new Uri($"wss://127.0.0.1:{credentials.Port}/");
            await ws.ConnectAsync(endpointUri, linkedCts.Token).ConfigureAwait(false);

            _webSocket = ws;
            _cts = new CancellationTokenSource();

            // 批量订阅业务白名单内的精准 Topic，杜绝全量通配引起的 LCU 序列化卡顿
            foreach (var uri in WampFrameParser.CoreSubscribedUris)
            {
                var subscribeMsg = WampFrameParser.CreateSubscribeMessage(uri);
                await ws.SendAsync(
                    subscribeMsg,
                    WebSocketMessageType.Text,
                    true,
                    _cts.Token).ConfigureAwait(false);
            }

            // 容量为 256 的单写单读有界队列，超出容量时 DropOldest 丢弃最旧快照。
            // 语义说明：LCU 推送的绝大多数数据帧为全量幂等快照（最新帧覆盖旧状态）；
            // 针对极少数理论上的 Delete 帧（如选人/对局关闭），由 Coordinator 轮询与 Gameflow 阶段状态转移提供双重对齐保证。
            var channel = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(256)
            {
                SingleWriter = true,
                SingleReader = true,
                FullMode = BoundedChannelFullMode.DropOldest
            });
            _frameChannel = channel;
            _processTask = Task.Run(() => ProcessFramesLoopAsync(channel.Reader, _cts.Token), CancellationToken.None);
            _receiveTask = Task.Run(ReceiveLoopAsync, CancellationToken.None);

            Connected?.Invoke();
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex);
            await CleanupWebSocketAsync().ConfigureAwait(false);
            throw;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed)
            return;

        await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await DisconnectCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async ValueTask SendAsync(
        ReadOnlyMemory<byte> message,
        WebSocketMessageType messageType = WebSocketMessageType.Text,
        bool endOfMessage = true,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var ws = _webSocket;
        if (ws is null || ws.State != WebSocketState.Open)
            throw new InvalidOperationException("WebSocket is not connected.");

        await ws.SendAsync(message, messageType, endOfMessage, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        try
        {
            _cts?.Cancel();
        }
        catch
        {
        }

        try
        {
            _webSocket?.Abort();
            _webSocket?.Dispose();
        }
        catch
        {
        }

        _cts?.Dispose();
        _connectionLock.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await DisconnectCoreAsync(CancellationToken.None).ConfigureAwait(false);
        _connectionLock.Dispose();
    }

    private async Task DisconnectCoreAsync(CancellationToken cancellationToken)
    {
        if (_cts is not null)
            await _cts.CancelAsync().ConfigureAwait(false);

        _frameChannel?.Writer.TryComplete();

        if (_webSocket is { State: WebSocketState.Open or WebSocketState.CloseReceived })
            try
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                using var linkedCts =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client disconnected", linkedCts.Token)
                    .ConfigureAwait(false);
            }
            catch
            {
            }

        if (_receiveTask is not null)
            try
            {
                await _receiveTask.ConfigureAwait(false);
            }
            catch
            {
            }

        if (_processTask is not null)
            try
            {
                await _processTask.ConfigureAwait(false);
            }
            catch
            {
            }

        await CleanupWebSocketAsync().ConfigureAwait(false);
        Disconnected?.Invoke(WebSocketCloseStatus.NormalClosure, "Client disconnected");
    }

    private async Task ReceiveLoopAsync()
    {
        var ws = _webSocket;
        var cts = _cts;
        var channel = _frameChannel;
        if (ws is null || cts is null || channel is null)
            return;

        var rentedBuffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        var messageAccumulator = new ArrayBufferWriter<byte>();

        try
        {
            while (!cts.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                var receiveResult = await ws.ReceiveAsync(rentedBuffer, cts.Token).ConfigureAwait(false);

                if (receiveResult.MessageType == WebSocketMessageType.Close)
                {
                    try
                    {
                        await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Acknowledged",
                            CancellationToken.None).ConfigureAwait(false);
                    }
                    catch
                    {
                    }

                    Disconnected?.Invoke(receiveResult.CloseStatus, receiveResult.CloseStatusDescription);
                    break;
                }

                if (receiveResult.Count == 0 && receiveResult.EndOfMessage)
                    // 忽略 LCU 对 [5, ...] 订阅请求返回的 0 字节确认响应
                    continue;

                if (receiveResult.EndOfMessage && messageAccumulator.WrittenCount == 0)
                {
                    // 极简零阻塞：仅复制接收帧并推入 Channel 队列，立即继续抽干 Socket
                    var frameBytes = rentedBuffer.AsSpan(0, receiveResult.Count).ToArray();
                    channel.Writer.TryWrite(frameBytes);
                }
                else
                {
                    messageAccumulator.Write(rentedBuffer.AsSpan(0, receiveResult.Count));
                    if (receiveResult.EndOfMessage)
                    {
                        if (messageAccumulator.WrittenCount > 0)
                        {
                            var frameBytes = messageAccumulator.WrittenSpan.ToArray();
                            channel.Writer.TryWrite(frameBytes);
                        }

                        messageAccumulator.Clear();
                    }
                }
            }
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex);
            Disconnected?.Invoke(WebSocketCloseStatus.InternalServerError, ex.Message);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rentedBuffer);
            channel.Writer.TryComplete();
        }
    }

    private async Task ProcessFramesLoopAsync(ChannelReader<byte[]> reader, CancellationToken ct)
    {
        try
        {
            while (await reader.WaitToReadAsync(ct).ConfigureAwait(false))
            while (reader.TryRead(out var frameBytes))
            {
                if (ct.IsCancellationRequested)
                    return;

                if (WampFrameParser.TryParseEventFrame(frameBytes, out _, out var uri, out var eventType, out var data,
                        out var doc))
                    using (doc)
                    {
                        _eventBus.Publish(uri!, eventType!, data);
                    }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ChannelClosedException)
        {
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex);
        }
    }

    private async Task CleanupWebSocketAsync()
    {
        _frameChannel?.Writer.TryComplete();
        _frameChannel = null;
        _processTask = null;
        _receiveTask = null;

        if (_webSocket is not null)
        {
            try
            {
                _webSocket.Dispose();
            }
            catch
            {
            }

            _webSocket = null;
        }

        if (_cts is not null)
        {
            _cts.Dispose();
            _cts = null;
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }
}