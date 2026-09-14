using System.Globalization;
using last.Core.State.Models;

namespace last.Core.State;

public sealed class GameDataState : IGameDataState
{
    private readonly Lock _lock = new();
    private Dictionary<int, ChampionSimple> _championMap = [];
    private IReadOnlyList<ChampionSimple> _champions = [];

    public event Action<IReadOnlyList<ChampionSimple>>? ChampionsChanged;

    public IReadOnlyList<ChampionSimple> Champions
    {
        get
        {
            lock (_lock)
            {
                return _champions;
            }
        }
    }

    public ChampionSimple? GetChampion(int championId)
    {
        lock (_lock)
        {
            return _championMap.GetValueOrDefault(championId);
        }
    }

    public string GetChampionName(int championId)
    {
        lock (_lock)
        {
            if (_championMap.TryGetValue(championId, out var champ) && !string.IsNullOrWhiteSpace(champ.Name))
                return champ.Name;

            return championId.ToString(CultureInfo.InvariantCulture);
        }
    }

    public void UpdateChampions(IReadOnlyList<ChampionSimple> champions)
    {
        ArgumentNullException.ThrowIfNull(champions);

        Action<IReadOnlyList<ChampionSimple>>? handler;
        IReadOnlyList<ChampionSimple> snapshot;

        lock (_lock)
        {
            var map = new Dictionary<int, ChampionSimple>(champions.Count);
            foreach (var champ in champions) map[champ.Id] = champ;

            _championMap = map;
            _champions = champions;
            snapshot = _champions;
            handler = ChampionsChanged;
        }

        handler?.Invoke(snapshot);
    }

    public void Reset()
    {
        Action<IReadOnlyList<ChampionSimple>>? handler;

        lock (_lock)
        {
            if (_champions.Count == 0) return;

            _championMap = [];
            _champions = [];
            handler = ChampionsChanged;
        }

        handler?.Invoke([]);
    }
}