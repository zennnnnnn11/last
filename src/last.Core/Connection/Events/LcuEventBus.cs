using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using last.Core.Connection.WebSocket;

namespace last.Core.Connection.Events;

public sealed class LcuEventBus : ILcuEventBus
{
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        TypeInfoResolver = LcuJsonSerializerContext.Default
    };

    private readonly Lock _gate = new();
    private readonly RadixMatcher<List<ILcuSubscription>> _matcher = new();
    private readonly JsonSerializerOptions _serializerOptions;

    public LcuEventBus(JsonSerializerOptions? serializerOptions = null)
    {
        _serializerOptions = serializerOptions ?? DefaultOptions;
    }

    public IDisposable Subscribe(string uriPattern, Action<LcuEvent> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uriPattern);
        ArgumentNullException.ThrowIfNull(handler);

        return RegisterSubscription(uriPattern, new SyncLcuSubscription(handler));
    }

    public IDisposable Subscribe<T>(string uriPattern, Action<LcuEvent<T>> handler)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uriPattern);
        ArgumentNullException.ThrowIfNull(handler);

        return RegisterSubscription(uriPattern, new SyncTypedLcuSubscription<T>(handler, _serializerOptions));
    }

    public void Publish(string uri, string eventType, JsonElement data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);

        (ILcuSubscription Sub, IReadOnlyDictionary<string, string>? Params)[] rented;
        var count = 0;

        lock (_gate)
        {
            var routes = _matcher.FindAll(uri);
            if (routes.Count == 0)
                return;

            var totalSubs = 0;
            for (var i = 0; i < routes.Count; i++)
                totalSubs += routes[i].Data.Count;

            if (totalSubs == 0)
                return;

            rented = ArrayPool<(ILcuSubscription, IReadOnlyDictionary<string, string>?)>.Shared.Rent(totalSubs);
            for (var i = 0; i < routes.Count; i++)
            {
                var route = routes[i];
                var subs = route.Data;
                for (var j = 0; j < subs.Count; j++)
                    rented[count++] = (subs[j], route.Parameters);
            }
        }

        try
        {
            for (var i = 0; i < count; i++)
            {
                var (sub, parameters) = rented[i];
                try
                {
                    sub.Invoke(uri, eventType, data, parameters);
                }
                catch (Exception ex)
                {
                    SubscriptionErrorOccurred?.Invoke(uri, ex);
                }
            }
        }
        finally
        {
            Array.Clear(rented, 0, count);
            ArrayPool<(ILcuSubscription, IReadOnlyDictionary<string, string>?)>.Shared.Return(rented);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _matcher.Clear();
        }
    }

    public event Action<string, Exception>? SubscriptionErrorOccurred;

    private LcuSubscriptionDisposable RegisterSubscription(string uriPattern, ILcuSubscription subscription)
    {
        lock (_gate)
        {
            var existing = _matcher.GetRouteData(uriPattern);
            if (existing is not null)
            {
                existing.Data.Add(subscription);
                _matcher.InvalidateCache();
            }
            else
            {
                _matcher.Insert(uriPattern, [subscription]);
            }
        }

        return new LcuSubscriptionDisposable(() =>
        {
            lock (_gate)
            {
                var existing = _matcher.GetRouteData(uriPattern);
                if (existing is not null)
                {
                    existing.Data.Remove(subscription);
                    if (existing.Data.Count == 0)
                        _matcher.Remove(uriPattern);
                    else
                        _matcher.InvalidateCache();
                }
            }
        });
    }

    private interface ILcuSubscription
    {
        void Invoke(string uri, string eventType, JsonElement data, IReadOnlyDictionary<string, string>? parameters);
    }

    private sealed class SyncLcuSubscription(Action<LcuEvent> handler) : ILcuSubscription
    {
        public void Invoke(string uri, string eventType, JsonElement data,
            IReadOnlyDictionary<string, string>? parameters)
        {
            handler(new LcuEvent(uri, eventType, data, parameters));
        }
    }

    private sealed class SyncTypedLcuSubscription<T>(Action<LcuEvent<T>> handler, JsonSerializerOptions? options)
        : ILcuSubscription
    {
        private readonly JsonTypeInfo<T>? _typeInfo =
            (options ?? DefaultOptions).TryGetTypeInfo(typeof(T), out var raw) ? raw as JsonTypeInfo<T> : null;

        public void Invoke(string uri, string eventType, JsonElement data,
            IReadOnlyDictionary<string, string>? parameters)
        {
            var typed = _typeInfo is not null ? data.Deserialize(_typeInfo) : DeserializeFallback<T>(data, options);
            handler(new LcuEvent<T>(uri, eventType, typed, parameters));
        }

        [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
            Justification = "Fallback only used when type is not in static context.")]
        [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
            Justification = "Fallback only used when type is not in static context.")]
        private static TVal? DeserializeFallback<TVal>(JsonElement element, JsonSerializerOptions? opt)
        {
            return element.Deserialize<TVal>(opt);
        }
    }

    private sealed class LcuSubscriptionDisposable(Action onDispose) : IDisposable
    {
        private Action? _onDispose = onDispose;

        public void Dispose()
        {
            Interlocked.Exchange(ref _onDispose, null)?.Invoke();
        }
    }
}