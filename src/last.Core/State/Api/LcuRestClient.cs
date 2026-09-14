using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using last.Core.Connection.Http;
using last.Core.Connection.Models;
using last.Core.Connection.WebSocket;

namespace last.Core.State.Api;

public sealed class LcuRestClient : ILcuRestClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        TypeInfoResolver = LcuJsonSerializerContext.Default
    };

    private static readonly SemaphoreSlim ApiRequestGate = new(8, 8);
    private static readonly SemaphoreSlim AssetRequestGate = new(24, 24);

    private readonly HttpClient? _externalClient;

    private readonly Lock _lock = new();
    private bool _disposed;
    private HttpClient? _internalClient;

    public LcuRestClient(HttpClient? externalClient = null)
    {
        _externalClient = externalClient;
    }

    public bool IsConfigured
    {
        get
        {
            lock (_lock)
            {
                return _externalClient is not null || _internalClient is not null;
            }
        }
    }

    public event Action<string, Exception>? RequestErrorOccurred;

    public void Configure(LcuCredentials credentials)
    {
        ArgumentNullException.ThrowIfNull(credentials);

        lock (_lock)
        {
            if (_disposed || _externalClient is not null)
                return;

            _internalClient?.Dispose();
            _internalClient = LcuHttpClientFactory.CreateLcuClient(credentials);
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            if (_externalClient is not null)
                return;

            _internalClient?.Dispose();
            _internalClient = null;
        }
    }

    public async Task<T?> GetAsync<T>(string uri, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);

        var client = GetActiveClient();
        if (client is null)
            return default;

        await ApiRequestGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return default;

            if (!response.IsSuccessStatusCode)
            {
                var errorEx = new HttpRequestException(
                    $"LCU GET '{uri}' failed with HTTP {(int)response.StatusCode} ({response.StatusCode}).",
                    null,
                    response.StatusCode);
                Trace.WriteLine($"[LcuRestClient] {errorEx.Message}");
                RequestErrorOccurred?.Invoke(uri, errorEx);
                return default;
            }

            if (response.Content.Headers.ContentLength == 0)
                return default;

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            if (stream.CanSeek && stream.Length == 0)
                return default;

            try
            {
                if (JsonOptions.TryGetTypeInfo(typeof(T), out var rawInfo) && rawInfo is JsonTypeInfo<T> typeInfo)
                    return await JsonSerializer.DeserializeAsync(stream, typeInfo, cancellationToken)
                        .ConfigureAwait(false);

                return await DeserializeFallbackAsync<T>(stream, cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException jsonEx)
            {
                Trace.WriteLine($"[LcuRestClient] Deserialization error for GET '{uri}': {jsonEx.Message}");
                RequestErrorOccurred?.Invoke(uri, jsonEx);
                return default;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ObjectDisposedException ode)
        {
            Trace.WriteLine($"[LcuRestClient] Client disposed during GET '{uri}': {ode.Message}");
            RequestErrorOccurred?.Invoke(uri, ode);
            return default;
        }
        catch (HttpRequestException httpEx)
        {
            Trace.WriteLine($"[LcuRestClient] Network error during GET '{uri}': {httpEx.Message}");
            RequestErrorOccurred?.Invoke(uri, httpEx);
            return default;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[LcuRestClient] Unexpected error during GET '{uri}': {ex.Message}");
            RequestErrorOccurred?.Invoke(uri, ex);
            return default;
        }
        finally
        {
            ApiRequestGate.Release();
        }
    }

    public async Task<byte[]?> GetByteArrayAsync(string uri, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);

        var client = GetActiveClient();
        if (client is null)
            return null;

        await AssetRequestGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            if (!response.IsSuccessStatusCode)
            {
                var errorEx = new HttpRequestException(
                    $"LCU GET byte array '{uri}' failed with HTTP {(int)response.StatusCode} ({response.StatusCode}).",
                    null,
                    response.StatusCode);
                Trace.WriteLine($"[LcuRestClient] {errorEx.Message}");
                RequestErrorOccurred?.Invoke(uri, errorEx);
                return null;
            }

            return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ObjectDisposedException ode)
        {
            Trace.WriteLine($"[LcuRestClient] Client disposed during GET byte array '{uri}': {ode.Message}");
            RequestErrorOccurred?.Invoke(uri, ode);
            return null;
        }
        catch (HttpRequestException httpEx)
        {
            Trace.WriteLine($"[LcuRestClient] Network error during GET byte array '{uri}': {httpEx.Message}");
            RequestErrorOccurred?.Invoke(uri, httpEx);
            return null;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[LcuRestClient] Unexpected error during GET byte array '{uri}': {ex.Message}");
            RequestErrorOccurred?.Invoke(uri, ex);
            return null;
        }
        finally
        {
            AssetRequestGate.Release();
        }
    }

    public async Task<bool> PostAsync(string uri, object? body = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);

        var client = GetActiveClient();
        if (client is null)
            return false;

        await ApiRequestGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string json;
            if (body is null)
                json = "{}";
            else if (body is string str)
                json = str;
            else if (JsonOptions.TryGetTypeInfo(body.GetType(), out var typeInfo))
                json = JsonSerializer.Serialize(body, typeInfo);
            else
                json = SerializeFallback(body);

            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await client.PostAsync(uri, content, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return false;

            if (!response.IsSuccessStatusCode)
            {
                var errorEx = new HttpRequestException(
                    $"LCU POST '{uri}' failed with HTTP {(int)response.StatusCode} ({response.StatusCode}).",
                    null,
                    response.StatusCode);
                Trace.WriteLine($"[LcuRestClient] {errorEx.Message}");
                RequestErrorOccurred?.Invoke(uri, errorEx);
                return false;
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ObjectDisposedException ode)
        {
            Trace.WriteLine($"[LcuRestClient] Client disposed during POST '{uri}': {ode.Message}");
            RequestErrorOccurred?.Invoke(uri, ode);
            return false;
        }
        catch (HttpRequestException httpEx)
        {
            Trace.WriteLine($"[LcuRestClient] Network error during POST '{uri}': {httpEx.Message}");
            RequestErrorOccurred?.Invoke(uri, httpEx);
            return false;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[LcuRestClient] Unexpected error during POST '{uri}': {ex.Message}");
            RequestErrorOccurred?.Invoke(uri, ex);
            return false;
        }
        finally
        {
            ApiRequestGate.Release();
        }
    }

    public async Task<bool> PatchAsync(string uri, object? body = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);

        var client = GetActiveClient();
        if (client is null)
            return false;

        await ApiRequestGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string json;
            if (body is null)
                json = "{}";
            else if (body is string str)
                json = str;
            else if (JsonOptions.TryGetTypeInfo(body.GetType(), out var typeInfo))
                json = JsonSerializer.Serialize(body, typeInfo);
            else
                json = SerializeFallback(body);

            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await client.PatchAsync(uri, content, cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return false;

            if (!response.IsSuccessStatusCode)
            {
                var errorEx = new HttpRequestException(
                    $"LCU PATCH '{uri}' failed with HTTP {(int)response.StatusCode} ({response.StatusCode}).",
                    null,
                    response.StatusCode);
                Trace.WriteLine($"[LcuRestClient] {errorEx.Message}");
                RequestErrorOccurred?.Invoke(uri, errorEx);
                return false;
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ObjectDisposedException ode)
        {
            Trace.WriteLine($"[LcuRestClient] Client disposed during PATCH '{uri}': {ode.Message}");
            RequestErrorOccurred?.Invoke(uri, ode);
            return false;
        }
        catch (HttpRequestException httpEx)
        {
            Trace.WriteLine($"[LcuRestClient] Network error during PATCH '{uri}': {httpEx.Message}");
            RequestErrorOccurred?.Invoke(uri, httpEx);
            return false;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[LcuRestClient] Unexpected error during PATCH '{uri}': {ex.Message}");
            RequestErrorOccurred?.Invoke(uri, ex);
            return false;
        }
        finally
        {
            ApiRequestGate.Release();
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
                return;

            _disposed = true;
            _internalClient?.Dispose();
            _internalClient = null;
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Fallback only used when type is not registered in static context.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "Fallback only used when type is not registered in static context.")]
    private static async Task<T?> DeserializeFallbackAsync<T>(Stream stream, CancellationToken cancellationToken)
    {
        return await JsonSerializer.DeserializeAsync<T>(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Fallback only used when type is not registered in static context.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "Fallback only used when type is not registered in static context.")]
    private static string SerializeFallback(object body)
    {
        return JsonSerializer.Serialize(body, body.GetType());
    }

    private HttpClient? GetActiveClient()
    {
        lock (_lock)
        {
            return _externalClient ?? _internalClient;
        }
    }
}