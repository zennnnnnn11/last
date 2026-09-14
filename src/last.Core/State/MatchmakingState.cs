using last.Core.State.Models;

namespace last.Core.State;

/// <summary>
///     匹配系统响应式状态容器实现，采用 System.Threading.Lock 提供线程安全保护，
///     锁外派发通知杜绝死锁。
/// </summary>
public sealed class MatchmakingState : IMatchmakingState
{
    private readonly Lock _lock = new();
    private ReadyCheck? _readyCheck;
    private MatchmakingSearch? _search;

    public ReadyCheck? ReadyCheck
    {
        get
        {
            lock (_lock)
            {
                return _readyCheck;
            }
        }
    }

    public MatchmakingSearch? Search
    {
        get
        {
            lock (_lock)
            {
                return _search;
            }
        }
    }

    public event Action<ReadyCheck?>? ReadyCheckChanged;

    public event Action<MatchmakingSearch?>? SearchChanged;

    public void SetReadyCheck(ReadyCheck? readyCheck)
    {
        bool changed;

        lock (_lock)
        {
            if (ReferenceEquals(_readyCheck, readyCheck) ||
                (_readyCheck is not null && _readyCheck.Equals(readyCheck))) return;

            _readyCheck = readyCheck;
            changed = true;
        }

        if (changed) ReadyCheckChanged?.Invoke(readyCheck);
    }

    public void SetSearch(MatchmakingSearch? search)
    {
        bool changed;

        lock (_lock)
        {
            if (ReferenceEquals(_search, search) || (_search is not null && _search.Equals(search))) return;

            _search = search;
            changed = true;
        }

        if (changed) SearchChanged?.Invoke(search);
    }

    public void Reset()
    {
        var readyCheckChanged = false;
        var searchChanged = false;

        lock (_lock)
        {
            if (_readyCheck is not null)
            {
                _readyCheck = null;
                readyCheckChanged = true;
            }

            if (_search is not null)
            {
                _search = null;
                searchChanged = true;
            }
        }

        if (readyCheckChanged) ReadyCheckChanged?.Invoke(null);

        if (searchChanged) SearchChanged?.Invoke(null);
    }
}