namespace last.Core.Connection.Events;

internal sealed class RadixMatcherNode<TData>
{
    public RadixMatcherNode(
        RadixMatcherNodeType type = RadixMatcherNodeType.Normal,
        RadixMatcherNode<TData>? parent = null,
        TData? data = default)
    {
        Type = type;
        Parent = parent;
        Data = data;
    }

    public RadixMatcherNodeType Type { get; }

    public RadixMatcherNode<TData>? Parent { get; }

    public TData? Data { get; set; }

    public Dictionary<string, RadixMatcherNode<TData>> NormalNodes { get; } = new(StringComparer.Ordinal);

    public RadixMatcherNode<TData>? WildcardNode { get; set; }

    public Dictionary<string, RadixMatcherNode<TData>> PlaceholderNodes { get; } = new(StringComparer.Ordinal);

    public static RadixMatcherNodeType InferNodeType(string node)
    {
        if (node.StartsWith("**", StringComparison.Ordinal))
            return RadixMatcherNodeType.Wildcard;

        if (node.StartsWith(':') || node == "*")
            return RadixMatcherNodeType.Placeholder;

        return RadixMatcherNodeType.Normal;
    }
}