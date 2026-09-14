namespace last.Core.Common;

/// <summary>
///     高性能、线程安全的泛型 LRU 缓存（上限淘汰，淘汰时仅解除引用，不主动调用 Dispose，交由 GC 自然回收）。
/// </summary>
public sealed class LruCache<TKey, TValue> where TKey : notnull
{
    private readonly Lock _gate = new();
    private readonly LinkedList<CacheEntry> _list = new();
    private readonly Dictionary<TKey, LinkedListNode<CacheEntry>> _map;

    public LruCache(int capacity = 256)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        Capacity = capacity;
        _map = new Dictionary<TKey, LinkedListNode<CacheEntry>>(capacity);
    }

    public int Capacity { get; }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _map.Count;
            }
        }
    }

    public bool TryGetValue(TKey key, out TValue? value)
    {
        lock (_gate)
        {
            if (_map.TryGetValue(key, out var node))
            {
                if (node != _list.First)
                {
                    _list.Remove(node);
                    _list.AddFirst(node);
                }

                value = node.Value.Value;
                return true;
            }

            value = default;
            return false;
        }
    }

    public void Set(TKey key, TValue value)
    {
        lock (_gate)
        {
            if (_map.TryGetValue(key, out var existingNode))
            {
                _list.Remove(existingNode);
            }
            else if (_map.Count >= Capacity)
            {
                // 超出上限：淘汰最久未被访问的尾部节点
                var lruNode = _list.Last;
                if (lruNode is not null)
                {
                    _map.Remove(lruNode.Value.Key);
                    _list.RemoveLast();
                }
            }

            var newNode = new LinkedListNode<CacheEntry>(new CacheEntry(key, value));
            _list.AddFirst(newNode);
            _map[key] = newNode;
        }
    }

    public bool Remove(TKey key)
    {
        lock (_gate)
        {
            if (_map.Remove(key, out var node))
            {
                _list.Remove(node);
                return true;
            }

            return false;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _map.Clear();
            _list.Clear();
        }
    }

    private readonly struct CacheEntry(TKey key, TValue value)
    {
        public TKey Key { get; } = key;
        public TValue Value { get; } = value;
    }
}