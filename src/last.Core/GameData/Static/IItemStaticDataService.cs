namespace last.Core.GameData.Static;

public interface IItemStaticDataService : IDisposable
{
    bool IsInitialized { get; }

    string GetItemIconUri(int itemId);

    ItemStaticInfo? GetItem(int itemId);

    string GetItemName(int itemId);

    IReadOnlyList<ItemStaticInfo> GetAllItems();

    Task InitializeAsync(CancellationToken cancellationToken = default);

    void UpdateFromLcu(IReadOnlyList<LcuItemDto> items);
}