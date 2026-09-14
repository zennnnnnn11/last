namespace last.Core.Connection.Events;

public sealed record FindResult<TData>(
    TData Data,
    IReadOnlyDictionary<string, string>? Parameters = null);