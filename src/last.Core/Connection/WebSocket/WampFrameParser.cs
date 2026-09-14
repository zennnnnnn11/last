using System.Text;
using System.Text.Json;

namespace last.Core.Connection.WebSocket;

public static class WampFrameParser
{
    /// <summary>
    ///     LCU 默认全量通配事件通道（仅作为向后兼容保留，生产中应优先使用精准 Topic）。
    /// </summary>
    public static readonly byte[] DefaultSubscribeMessage = "[5, \"OnJsonApiEvent\"]"u8.ToArray();

    /// <summary>
    ///     系统业务运行真正依赖的核心 URI 精确订阅白名单，杜绝全量通配导致的 LCU 客户端严重卡顿与无用数据流风暴。
    /// </summary>
    public static readonly IReadOnlyList<string> CoreSubscribedUris =
    [
        "/lol-gameflow/v1/gameflow-phase",
        "/lol-gameflow/v1/session",
        "/lol-summoner/v1/current-summoner",
        "/lol-summoner/v1/current-summoner/summoner-profile",
        "/lol-matchmaking/v1/ready-check",
        "/lol-matchmaking/v1/search",
        "/lol-champ-select/v1/session",
        "/lol-champ-select/v1/current-champion",
        "/lol-lobby-team-builder/champ-select/v1/subset-champion-list",
        "/lol-pre-end-of-game/v1/currentSequenceEvent"
    ];

    /// <summary>
    ///     将 LCU REST 格式 URI（如 "/lol-gameflow/v1/gameflow-phase"）转换为 LCU 规范的 WAMP Topic 名称（如
    ///     "OnJsonApiEvent_lol-gameflow_v1_gameflow-phase"）。
    /// </summary>
    public static string FormatTopic(string uri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);
        return "OnJsonApiEvent_" + uri.TrimStart('/').Replace('/', '_');
    }

    /// <summary>
    ///     为指定的目标 URI 构建 WAMP v1 SUBSCRIBE 订阅数据包。
    /// </summary>
    public static byte[] CreateSubscribeMessage(string uri)
    {
        var topic = FormatTopic(uri);
        return Encoding.UTF8.GetBytes($"[5, \"{topic}\"]");
    }

    /// <summary>
    ///     高性能解析 WAMP v1 EVENT 帧。采用 Utf8JsonReader 流式提取协议信封，
    ///     仅对实际承载业务的 data 节点执行单次建树，彻底杜绝外层 ParseValue 与 data.Clone() 的双重内存拷贝开销。
    /// </summary>
    public static bool TryParseEventFrame(
        ReadOnlySpan<byte> utf8Json,
        out string? topic,
        out string? uri,
        out string? eventType,
        out JsonElement data)
    {
        return TryParseEventFrame(utf8Json, out topic, out uri, out eventType, out data, out _);
    }

    /// <summary>
    ///     高性能解析 WAMP v1 EVENT 帧，并暴露底层 JsonDocument 以供调用方在生命周期结束后显式回收池化内存。
    /// </summary>
    public static bool TryParseEventFrame(
        ReadOnlySpan<byte> utf8Json,
        out string? topic,
        out string? uri,
        out string? eventType,
        out JsonElement data,
        out JsonDocument? document)
    {
        topic = null;
        uri = null;
        eventType = null;
        data = default;
        document = null;

        if (utf8Json.IsEmpty)
            return false;

        try
        {
            var reader = new Utf8JsonReader(utf8Json);

            // 1. 根节点必须为 WAMP 协议数组：[OpCode, Topic, Payload]
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartArray)
                return false;

            // 2. OpCode 校验：8 为 WAMP v1 EVENT 帧
            if (!reader.Read() || reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out var opCode) ||
                opCode != 8)
                return false;

            // 3. Topic 校验
            if (!reader.Read() || reader.TokenType != JsonTokenType.String)
                return false;

            topic = reader.GetString();

            // 4. Payload 必须为 Object：{"data": ..., "eventType": "...", "uri": "..."}
            if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
                return false;

            var propDepth = reader.CurrentDepth + 1;

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject && reader.CurrentDepth < propDepth)
                    break;

                if (reader.TokenType == JsonTokenType.PropertyName && reader.CurrentDepth == propDepth)
                {
                    if (reader.ValueTextEquals("uri"u8))
                    {
                        if (!reader.Read() || reader.TokenType != JsonTokenType.String)
                            goto Failed;

                        uri = reader.GetString();
                    }
                    else if (reader.ValueTextEquals("eventType"u8))
                    {
                        if (!reader.Read() || reader.TokenType != JsonTokenType.String)
                            goto Failed;

                        eventType = reader.GetString();
                    }
                    else if (reader.ValueTextEquals("data"u8))
                    {
                        if (!reader.Read())
                            goto Failed;

                        if (reader.TokenType != JsonTokenType.Null)
                        {
                            document?.Dispose();
                            document = JsonDocument.ParseValue(ref reader);
                            data = document.RootElement;
                        }
                    }
                    else
                    {
                        reader.Skip();
                    }
                }
            }

            if (string.IsNullOrEmpty(uri) || string.IsNullOrEmpty(eventType))
                goto Failed;

            return true;

        Failed:
            topic = null;
            uri = null;
            eventType = null;
            data = default;
            document?.Dispose();
            document = null;
            return false;
        }
        catch
        {
            topic = null;
            uri = null;
            eventType = null;
            data = default;
            document?.Dispose();
            document = null;
            return false;
        }
    }
}