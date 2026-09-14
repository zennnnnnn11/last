using System.Globalization;
using last.Core.State.Api;

namespace last.Core.GameData.Static;

public sealed class ItemStaticDataService : IItemStaticDataService
{
    private readonly IGameDataApi? _gameDataApi;
    private readonly Lock _lock = new();

    private Dictionary<int, ItemStaticInfo> _items = [];

    public ItemStaticDataService(IGameDataApi? gameDataApi = null)
    {
        _gameDataApi = gameDataApi;
    }

    public bool IsInitialized { get; private set; }

    public string GetItemIconUri(int itemId)
    {
        return ItemStaticDataDefaults.GetGtimgIconUri(itemId);
    }

    public ItemStaticInfo? GetItem(int itemId)
    {
        lock (_lock)
        {
            return _items.GetValueOrDefault(itemId);
        }
    }

    public string GetItemName(int itemId)
    {
        lock (_lock)
        {
            if (_items.TryGetValue(itemId, out var item) && !string.IsNullOrWhiteSpace(item.Name))
                return item.Name;

            return itemId.ToString(CultureInfo.InvariantCulture);
        }
    }

    public IReadOnlyList<ItemStaticInfo> GetAllItems()
    {
        lock (_lock)
        {
            return _items.Values.ToList();
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_gameDataApi == null)
            return;

        try
        {
            var lcuItems = await _gameDataApi.GetItemsAsync(cancellationToken).ConfigureAwait(false);
            if (lcuItems.Count > 0)
                UpdateFromLcu(lcuItems);
        }
        catch
        {
        }
    }

    public void UpdateFromLcu(IReadOnlyList<LcuItemDto> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var dict = new Dictionary<int, ItemStaticInfo>(items.Count);
        foreach (var dto in items)
        {
            if (dto.Id <= 0)
                continue;

            var info = new ItemStaticInfo(
                dto.Id,
                dto.Name ?? string.Empty,
                dto.Description ?? string.Empty,
                dto.PriceTotal,
                dto.From ?? [],
                dto.To ?? [],
                dto.Categories ?? [],
                dto.IconPath ?? string.Empty
            );
            dict[dto.Id] = info;
        }

        lock (_lock)
        {
            _items = dict;
            IsInitialized = true;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _items.Clear();
            IsInitialized = false;
        }
    }
}