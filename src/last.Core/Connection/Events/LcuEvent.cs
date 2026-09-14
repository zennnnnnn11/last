using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using last.Core.Connection.WebSocket;

namespace last.Core.Connection.Events;

public sealed record LcuEvent(
    string Uri,
    string EventType,
    JsonElement Data,
    IReadOnlyDictionary<string, string>? Parameters = null)
{
    public T? GetData<T>(JsonSerializerOptions? options = null)
    {
        if (Data.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            return default;

        var opt = options ?? LcuJsonSerializerContext.Default.Options;
        if (opt.TryGetTypeInfo(typeof(T), out var raw) && raw is JsonTypeInfo<T> typeInfo)
            return Data.Deserialize(typeInfo);

        return DeserializeFallback<T>(opt);
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
        Justification = "Fallback only used when type is not in static context.")]
    [UnconditionalSuppressMessage("AOT", "IL3050:RequiresDynamicCode",
        Justification = "Fallback only used when type is not in static context.")]
    private T? DeserializeFallback<T>(JsonSerializerOptions options)
    {
        return Data.Deserialize<T>(options);
    }
}

public sealed record LcuEvent<T>(
    string Uri,
    string EventType,
    T? Data,
    IReadOnlyDictionary<string, string>? Parameters = null);