using System.Text.Json;

namespace last.Core.Connection.Events;

public interface ILcuEventBus
{
    IDisposable Subscribe(string uriPattern, Action<LcuEvent> handler);

    IDisposable Subscribe<T>(string uriPattern, Action<LcuEvent<T>> handler);

    void Publish(string uri, string eventType, JsonElement data);

    void Clear();
}