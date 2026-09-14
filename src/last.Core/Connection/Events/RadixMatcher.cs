using System.Buffers;

namespace last.Core.Connection.Events;

public sealed class RadixMatcher<TData> where TData : class
{
    private const int MaxCacheEntries = 512;
    private readonly Dictionary<string, IReadOnlyList<FindResult<TData>>> _findAllCache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FindResult<TData>?> _findOneCache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TData> _staticRouteMap = new(StringComparer.Ordinal);
    private RadixMatcherNode<TData> _root = new();

    public void InvalidateCache()
    {
        _findAllCache.Clear();
        _findOneCache.Clear();
    }

    private void CacheFindAll(string path, IReadOnlyList<FindResult<TData>> results)
    {
        if (_findAllCache.Count >= MaxCacheEntries)
            EvictOldestEntries(_findAllCache, MaxCacheEntries / 2);

        _findAllCache[path] = results;
    }

    private void CacheFindOne(string path, FindResult<TData>? result)
    {
        if (_findOneCache.Count >= MaxCacheEntries)
            EvictOldestEntries(_findOneCache, MaxCacheEntries / 2);

        _findOneCache[path] = result;
    }

    private static void EvictOldestEntries<TValue>(Dictionary<string, TValue> cache, int evictCount)
    {
        var keysToRemove = ArrayPool<string>.Shared.Rent(evictCount);
        try
        {
            var count = 0;
            foreach (var key in cache.Keys)
            {
                keysToRemove[count++] = key;
                if (count >= evictCount)
                    break;
            }

            for (var i = 0; i < count; i++)
                cache.Remove(keysToRemove[i]);
        }
        finally
        {
            ArrayPool<string>.Shared.Return(keysToRemove, true);
        }
    }

    public static void ValidateRoute(string route)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(route);

        var parts = route.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            throw new ArgumentException("route should not be empty", nameof(route));

        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (part.StartsWith(':'))
            {
                if (part.Length == 1)
                    throw new ArgumentException("placeholder should have a name", nameof(route));
            }
            else if (part.StartsWith("**", StringComparison.Ordinal))
            {
                if (part.Length != 2)
                    throw new ArgumentException("wildcard should be **", nameof(route));

                if (i != parts.Length - 1)
                    throw new ArgumentException("wildcard should be the last part", nameof(route));
            }
            else if (part.StartsWith('*'))
            {
                if (part.Length != 1)
                    throw new ArgumentException("placeholder * should be the only part", nameof(route));
            }
            else
            {
                if (part.Contains(':') || part.Contains('*'))
                    throw new ArgumentException("normal nodes should not have : or *", nameof(route));
            }
        }
    }

    private static bool MaybeDynamicRoute(string route)
    {
        return route.Contains('*') || route.Contains(':');
    }

    public void Insert(string route, TData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        ValidateRoute(route);

        if (!MaybeDynamicRoute(route))
        {
            if (!_staticRouteMap.TryAdd(route, data))
                throw new InvalidOperationException($"route '{route}' already exists");

            InvalidateCache();
            return;
        }

        var parts = route.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var node = _root;
        var nextUnnamedPartCount = 0;

        foreach (var part in parts)
        {
            var type = RadixMatcherNode<TData>.InferNodeType(part);
            if (type == RadixMatcherNodeType.Normal)
            {
                if (!node.NormalNodes.TryGetValue(part, out var childNode))
                {
                    childNode = new RadixMatcherNode<TData>(type, node);
                    node.NormalNodes[part] = childNode;
                }

                node = childNode;
            }
            else if (type == RadixMatcherNodeType.Placeholder)
            {
                var key = part == "*" ? $"_{nextUnnamedPartCount++}" : part[1..];
                if (!node.PlaceholderNodes.TryGetValue(key, out var childNode))
                {
                    childNode = new RadixMatcherNode<TData>(type, node);
                    node.PlaceholderNodes[key] = childNode;
                }

                node = childNode;
            }
            else if (type == RadixMatcherNodeType.Wildcard)
            {
                node.WildcardNode ??= new RadixMatcherNode<TData>(type, node);

                node = node.WildcardNode;
            }
        }

        if (node.Data is not null)
            throw new InvalidOperationException($"route '{route}' already exists");

        node.Data = data;
        InvalidateCache();
    }

    private RadixMatcherNode<TData>? FindDynamicRouteNode(string route)
    {
        var parts = route.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var node = _root;
        var unnamedCount = 0;
        var i = 0;

        for (; i < parts.Length; i++)
        {
            var part = parts[i];
            var type = RadixMatcherNode<TData>.InferNodeType(part);
            if (type == RadixMatcherNodeType.Normal)
            {
                if (node.NormalNodes.TryGetValue(part, out var childNode))
                    node = childNode;
                else
                    break;
            }
            else if (type == RadixMatcherNodeType.Placeholder)
            {
                var key = part == "*" ? $"_{unnamedCount++}" : part[1..];
                if (node.PlaceholderNodes.TryGetValue(key, out var childNode))
                    node = childNode;
                else
                    break;
            }
            else if (type == RadixMatcherNodeType.Wildcard)
            {
                if (node.WildcardNode is not null)
                    node = node.WildcardNode;

                break;
            }
        }

        if ((i != parts.Length && node.Type != RadixMatcherNodeType.Wildcard) || node.Data is null)
            return null;

        return node;
    }

    public bool Remove(string route)
    {
        if (_staticRouteMap.Remove(route))
        {
            InvalidateCache();
            return true;
        }

        var node = FindDynamicRouteNode(route);
        if (node is null || node.Data is null)
            return false;

        node.Data = default;
        InvalidateCache();

        if (node.NormalNodes.Count > 0 || node.PlaceholderNodes.Count > 0 || node.WildcardNode is not null)
            return true;

        var current = node;
        while (current.Parent is not null)
        {
            var parent = current.Parent;

            if (current.Type == RadixMatcherNodeType.Normal)
            {
                foreach (var (key, child) in parent.NormalNodes)
                    if (ReferenceEquals(child, current))
                    {
                        parent.NormalNodes.Remove(key);
                        break;
                    }
            }
            else if (current.Type == RadixMatcherNodeType.Placeholder)
            {
                foreach (var (key, child) in parent.PlaceholderNodes)
                    if (ReferenceEquals(child, current))
                    {
                        parent.PlaceholderNodes.Remove(key);
                        break;
                    }
            }
            else if (current.Type == RadixMatcherNodeType.Wildcard)
            {
                if (ReferenceEquals(parent.WildcardNode, current))
                    parent.WildcardNode = null;
            }

            if (parent.Data is not null ||
                parent.NormalNodes.Count > 0 ||
                parent.PlaceholderNodes.Count > 0 ||
                parent.WildcardNode is not null)
                break;

            current = parent;
        }

        return true;
    }

    public FindResult<TData>? GetRouteData(string route)
    {
        if (_staticRouteMap.TryGetValue(route, out var staticData))
            return new FindResult<TData>(staticData);

        var node = FindDynamicRouteNode(route);
        if (node is not null && node.Data is not null)
            return new FindResult<TData>(node.Data);

        return null;
    }

    private static void PushWildcardMatch(
        RadixMatcherNode<TData> wildcardNode,
        string path,
        ReadOnlySpan<Range> ranges,
        int index,
        List<FindResult<TData>> result,
        List<KeyValuePair<string, string>>? parameters)
    {
        if (wildcardNode.Data is null)
            return;

        var paramCount = (parameters?.Count ?? 0) + 1;
        var dict = new Dictionary<string, string>(paramCount, StringComparer.Ordinal);

        if (index < ranges.Length)
        {
            var start = ranges[index].Start.Value;
            var end = ranges[^1].End.Value;
            dict["__"] = path[start..end];
        }
        else
        {
            dict["__"] = string.Empty;
        }

        if (parameters is not null)
            foreach (var kvp in parameters)
                dict[kvp.Key] = kvp.Value;

        result.Add(new FindResult<TData>(wildcardNode.Data, dict));
    }

    private static int SplitPath(string path, Span<Range> destination)
    {
        var count = 0;
        var start = 0;
        for (var i = 0; i < path.Length; i++)
            if (path[i] == '/')
            {
                if (i > start)
                {
                    if (count < destination.Length)
                        destination[count] = new Range(start, i);
                    count++;
                }

                start = i + 1;
            }

        if (path.Length > start)
        {
            if (count < destination.Length)
                destination[count] = new Range(start, path.Length);
            count++;
        }

        return count;
    }

    private List<FindResult<TData>> FindDynamicFast(string path, List<FindResult<TData>>? result, bool onlyOne)
    {
        Span<Range> stackRanges = stackalloc Range[32];
        var rangeCount = SplitPath(path, stackRanges);
        result ??= new List<FindResult<TData>>(onlyOne ? 1 : 4);

        if (rangeCount <= stackRanges.Length)
        {
            FindDynamicRecursive(_root, path, stackRanges[..rangeCount], 0, result, null, onlyOne);
            return result;
        }

        var rented = ArrayPool<Range>.Shared.Rent(rangeCount);
        try
        {
            SplitPath(path, rented.AsSpan(0, rangeCount));
            FindDynamicRecursive(_root, path, rented.AsSpan(0, rangeCount), 0, result, null, onlyOne);
            return result;
        }
        finally
        {
            ArrayPool<Range>.Shared.Return(rented);
        }
    }

    private static void FindDynamicRecursive(
        RadixMatcherNode<TData> node,
        string path,
        ReadOnlySpan<Range> ranges,
        int index,
        List<FindResult<TData>> result,
        List<KeyValuePair<string, string>>? parameters,
        bool onlyOne = false)
    {
        if (onlyOne && result.Count > 0)
            return;

        if (!onlyOne && node.WildcardNode is not null && node.WildcardNode.Data is not null && index < ranges.Length)
            PushWildcardMatch(node.WildcardNode, path, ranges, index, result, parameters);

        if (index == ranges.Length)
        {
            if (node.Data is not null)
            {
                var dict = BuildParametersDict(parameters);
                result.Add(new FindResult<TData>(node.Data, dict));
            }

            if (onlyOne && result.Count > 0)
                return;

            if (node.WildcardNode is not null && node.WildcardNode.Data is not null)
            {
                var dict = BuildParametersDict(parameters);
                result.Add(new FindResult<TData>(node.WildcardNode.Data, dict));
            }

            return;
        }

        var partSpan = path.AsSpan(ranges[index]);
        var normalLookup = node.NormalNodes.GetAlternateLookup<ReadOnlySpan<char>>();

        if (normalLookup.TryGetValue(partSpan, out var normalNode))
            FindDynamicRecursive(normalNode, path, ranges, index + 1, result, parameters, onlyOne);

        if (onlyOne && result.Count > 0)
            return;

        if (node.PlaceholderNodes.Count > 0)
        {
            parameters ??= new List<KeyValuePair<string, string>>();
            var partString = path[ranges[index]];
            foreach (var (key, childNode) in node.PlaceholderNodes)
            {
                parameters.Add(new KeyValuePair<string, string>(key, partString));
                FindDynamicRecursive(childNode, path, ranges, index + 1, result, parameters, onlyOne);
                parameters.RemoveAt(parameters.Count - 1);

                if (onlyOne && result.Count > 0)
                    return;
            }
        }

        if (onlyOne && node.WildcardNode is not null && node.WildcardNode.Data is not null)
            PushWildcardMatch(node.WildcardNode, path, ranges, index, result, parameters);
    }

    private static Dictionary<string, string>? BuildParametersDict(
        List<KeyValuePair<string, string>>? parameters)
    {
        if (parameters is null || parameters.Count == 0)
            return null;

        var dict = new Dictionary<string, string>(parameters.Count, StringComparer.Ordinal);
        foreach (var kvp in parameters)
            dict[kvp.Key] = kvp.Value;

        return dict;
    }

    public FindResult<TData>? FindOne(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        if (_findOneCache.TryGetValue(path, out var cached))
            return cached;

        if (_staticRouteMap.TryGetValue(path, out var staticData))
        {
            var res = new FindResult<TData>(staticData);
            CacheFindOne(path, res);
            return res;
        }

        var result = new List<FindResult<TData>>(1);
        result = FindDynamicFast(path, result, true);
        var single = result is { Count: > 0 } ? result[0] : null;
        CacheFindOne(path, single);
        return single;
    }

    public IReadOnlyList<FindResult<TData>> FindAll(string path)
    {
        if (string.IsNullOrEmpty(path))
            return Array.Empty<FindResult<TData>>();

        if (_findAllCache.TryGetValue(path, out var cached))
            return cached;

        List<FindResult<TData>>? result = null;

        if (_staticRouteMap.TryGetValue(path, out var staticData))
            result = [new FindResult<TData>(staticData)];

        result = FindDynamicFast(path, result, false);
        IReadOnlyList<FindResult<TData>> finalized = result is { Count: > 0 }
            ? result.ToArray()
            : Array.Empty<FindResult<TData>>();

        CacheFindAll(path, finalized);
        return finalized;
    }

    public void Clear()
    {
        _root = new RadixMatcherNode<TData>();
        _staticRouteMap.Clear();
        InvalidateCache();
    }
}