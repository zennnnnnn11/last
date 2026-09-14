using last.Core.Connection.Models;

namespace last.Core.State.Api;

public interface ILcuRestClient : IDisposable
{
    bool IsConfigured { get; }

    void Configure(LcuCredentials credentials);

    void Reset();

    Task<T?> GetAsync<T>(string uri, CancellationToken cancellationToken = default);

    Task<byte[]?> GetByteArrayAsync(string uri, CancellationToken cancellationToken = default);

    Task<bool> PostAsync(string uri, object? body = null, CancellationToken cancellationToken = default);

    Task<bool> PatchAsync(string uri, object? body = null, CancellationToken cancellationToken = default);

    /// <summary>
    ///     当向 LCU 发送 REST 请求发生网络、授权、解析或生命周期异常时触发，用于可观测性追踪与诊断。
    ///     注意：404 Not Found 属于资源未激活的常规语义响应，不视作异常。
    /// </summary>
    event Action<string, Exception>? RequestErrorOccurred;
}