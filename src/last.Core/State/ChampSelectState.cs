using last.Core.State.Models;

namespace last.Core.State;

public sealed class ChampSelectState : IChampSelectState
{
    private readonly Lock _lock = new();
    private int? _currentChampion;
    private ChampSelectSession? _session;
    private IReadOnlyList<int>? _subsetChampionList;

    public ChampSelectSession? Session
    {
        get
        {
            lock (_lock)
            {
                return _session;
            }
        }
    }

    public int? CurrentChampion
    {
        get
        {
            lock (_lock)
            {
                return _currentChampion;
            }
        }
    }

    public IReadOnlyList<BenchChampion>? BenchChampions
    {
        get
        {
            lock (_lock)
            {
                return _session?.BenchChampions;
            }
        }
    }

    public IReadOnlyList<int>? SubsetChampionList
    {
        get
        {
            lock (_lock)
            {
                return _subsetChampionList;
            }
        }
    }

    public event Action<ChampSelectSession?>? SessionChanged;

    public event Action<int?>? CurrentChampionChanged;

    public event Action<IReadOnlyList<BenchChampion>?>? BenchChampionsChanged;

    public event Action<IReadOnlyList<int>?>? SubsetChampionListChanged;

    public void SetSession(ChampSelectSession? session)
    {
        bool sessionChanged;
        bool benchChanged;
        IReadOnlyList<BenchChampion>? newBench;
        var championChanged = false;
        int? newChamp = null;

        lock (_lock)
        {
            if (ReferenceEquals(_session, session) || (_session is not null && _session.Equals(session)))
                return;

            var oldSession = _session;
            _session = session;
            sessionChanged = true;

            var oldBench = oldSession?.BenchChampions;
            newBench = session?.BenchChampions;
            benchChanged = !AreBenchChampionsEqual(oldBench, newBench);

            var sessionLocalChampId = session?.LocalPlayerChampionId ?? 0;
            if (sessionLocalChampId > 0 && _currentChampion != sessionLocalChampId)
            {
                _currentChampion = sessionLocalChampId;
                newChamp = sessionLocalChampId;
                championChanged = true;
            }
            else if (session is null && _currentChampion is not null)
            {
                _currentChampion = null;
                newChamp = null;
                championChanged = true;
            }
        }

        if (sessionChanged)
            SessionChanged?.Invoke(session);

        if (benchChanged)
            BenchChampionsChanged?.Invoke(newBench);

        if (championChanged)
            CurrentChampionChanged?.Invoke(newChamp);
    }

    public void SetCurrentChampion(int? championId)
    {
        bool changed;

        lock (_lock)
        {
            if (_currentChampion == championId)
                return;

            _currentChampion = championId;
            changed = true;
        }

        if (changed)
            CurrentChampionChanged?.Invoke(championId);
    }

    public void SetSubsetChampionList(IReadOnlyList<int>? subsetChampionList)
    {
        bool changed;
        lock (_lock)
        {
            if (ReferenceEquals(_subsetChampionList, subsetChampionList) ||
                (_subsetChampionList is not null && subsetChampionList is not null &&
                 _subsetChampionList.SequenceEqual(subsetChampionList)))
                return;

            _subsetChampionList = subsetChampionList;
            changed = true;
        }

        if (changed)
            SubsetChampionListChanged?.Invoke(subsetChampionList);
    }

    public void Reset()
    {
        var sessionChanged = false;
        var benchChanged = false;
        var champChanged = false;
        var subsetChanged = false;

        lock (_lock)
        {
            if (_session is not null)
            {
                benchChanged = _session.BenchChampions is not null && _session.BenchChampions.Count > 0;
                _session = null;
                sessionChanged = true;
            }

            if (_currentChampion is not null)
            {
                _currentChampion = null;
                champChanged = true;
            }

            if (_subsetChampionList is not null)
            {
                _subsetChampionList = null;
                subsetChanged = true;
            }
        }

        if (sessionChanged)
            SessionChanged?.Invoke(null);

        if (benchChanged)
            BenchChampionsChanged?.Invoke(null);

        if (champChanged)
            CurrentChampionChanged?.Invoke(null);

        if (subsetChanged)
            SubsetChampionListChanged?.Invoke(null);
    }

    private static bool AreBenchChampionsEqual(IReadOnlyList<BenchChampion>? list1, IReadOnlyList<BenchChampion>? list2)
    {
        if (ReferenceEquals(list1, list2))
            return true;

        if (list1 is null || list2 is null)
            return false;

        if (list1.Count != list2.Count)
            return false;

        for (var i = 0; i < list1.Count; i++)
        {
            var b1 = list1[i];
            var b2 = list2[i];
            if (b1.ChampionId != b2.ChampionId || b1.IsPriority != b2.IsPriority)
                return false;
        }

        return true;
    }
}