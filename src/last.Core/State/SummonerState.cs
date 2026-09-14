using last.Core.State.Models;

namespace last.Core.State;

/// <summary>
///     客户端召唤师状态容器实现，采用 System.Threading.Lock 保障多线程读取与写入安全。
/// </summary>
public sealed class SummonerState : ISummonerState
{
    private readonly Lock _lock = new();
    private SummonerInfo? _me;
    private bool _newIdSystemEnabled;
    private SummonerProfile? _profile;

    public SummonerInfo? Me
    {
        get
        {
            lock (_lock)
            {
                return _me;
            }
        }
    }

    public SummonerProfile? Profile
    {
        get
        {
            lock (_lock)
            {
                return _profile;
            }
        }
    }

    public bool NewIdSystemEnabled
    {
        get
        {
            lock (_lock)
            {
                return _newIdSystemEnabled;
            }
        }
    }

    public event Action<SummonerInfo?>? CurrentSummonerChanged;

    public event Action<SummonerProfile?>? ProfileChanged;

    public void SetMe(SummonerInfo? value)
    {
        bool changed;

        lock (_lock)
        {
            if (ReferenceEquals(_me, value) || (_me is not null && _me.Equals(value))) return;

            _me = value;
            _newIdSystemEnabled = !string.IsNullOrWhiteSpace(value?.TagLine);
            changed = true;
        }

        if (changed) CurrentSummonerChanged?.Invoke(value);
    }

    public void SetProfile(SummonerProfile? value)
    {
        bool changed;

        lock (_lock)
        {
            if (ReferenceEquals(_profile, value) || (_profile is not null && _profile.Equals(value))) return;

            _profile = value;
            changed = true;
        }

        if (changed) ProfileChanged?.Invoke(value);
    }

    public void Reset()
    {
        var meChanged = false;
        var profileChanged = false;

        lock (_lock)
        {
            if (_me is not null)
            {
                _me = null;
                meChanged = true;
            }

            if (_profile is not null)
            {
                _profile = null;
                profileChanged = true;
            }

            _newIdSystemEnabled = false;
        }

        if (meChanged) CurrentSummonerChanged?.Invoke(null);

        if (profileChanged) ProfileChanged?.Invoke(null);
    }
}