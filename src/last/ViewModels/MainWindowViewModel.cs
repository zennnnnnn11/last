using System;
using CommunityToolkit.Mvvm.ComponentModel;
using last.Core.Connection.Services;

namespace last.ViewModels;

public sealed class MainWindowViewModel : ObservableObject, IDisposable
{
    private bool _disposed;

    public MainWindowViewModel(ILcuConnectionCoordinator coordinator)
    {
        ArgumentNullException.ThrowIfNull(coordinator);

        Profile = new ProfileHeaderViewModel(coordinator);
        MatchHistoryCard = new MatchHistoryCardViewModel(coordinator);
        DeckCard = new DeckCardViewModel(coordinator, Profile, MatchHistoryCard);
        TeammateCard = new TeammateCardViewModel(coordinator, MatchHistoryCard);
    }

    public ProfileHeaderViewModel Profile { get; }

    public DeckCardViewModel DeckCard { get; }

    public TeammateCardViewModel TeammateCard { get; }

    public MatchHistoryCardViewModel MatchHistoryCard { get; }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        MatchHistoryCard.Dispose();
        TeammateCard.Dispose();
        DeckCard.Dispose();
        Profile.Dispose();
    }
}