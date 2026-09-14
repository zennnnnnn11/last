using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using last.Core.Connection.Models;
using last.Core.Connection.Services;
using last.Core.State.Models;
using last.Services;

namespace last.ViewModels;

public sealed partial class ProfileHeaderViewModel : ObservableObject, IDisposable
{
    private readonly ILcuConnectionCoordinator _coordinator;
    private CancellationTokenSource? _avatarCts;
    private int _currentIconId = -1;
    private bool _disposed;

    public ProfileHeaderViewModel(ILcuConnectionCoordinator coordinator)
    {
        _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));

        _coordinator.Summoner.CurrentSummonerChanged += OnSummonerChanged;
        _coordinator.Detector.StatusChanged += OnStatusChanged;

        UpdateFromSummoner(_coordinator.Summoner.Me);
        UpdateStatus(_coordinator.Detector.Status);
    }

    [ObservableProperty] public partial string DisplayName { get; set; } = "未连接";

    [ObservableProperty] public partial string TagLine { get; set; } = string.Empty;

    [ObservableProperty] public partial int Level { get; set; }

    [ObservableProperty] public partial bool HasTagLine { get; set; }

    [ObservableProperty] public partial bool IsConnected { get; set; }

    [ObservableProperty] public partial Bitmap? AvatarBitmap { get; set; }

    [ObservableProperty] public partial bool HasAvatar { get; set; }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _coordinator.Summoner.CurrentSummonerChanged -= OnSummonerChanged;
        _coordinator.Detector.StatusChanged -= OnStatusChanged;

        ClearAvatar();
    }

    private void OnSummonerChanged(SummonerInfo? summoner)
    {
        if (Dispatcher.UIThread.CheckAccess())
            UpdateFromSummoner(summoner);
        else
            Dispatcher.UIThread.Post(() => UpdateFromSummoner(summoner));
    }

    private void OnStatusChanged(ClientConnectionStatus status)
    {
        if (Dispatcher.UIThread.CheckAccess())
            UpdateStatus(status);
        else
            Dispatcher.UIThread.Post(() => UpdateStatus(status));
    }

    private void UpdateFromSummoner(SummonerInfo? summoner)
    {
        if (summoner is not null && !string.IsNullOrWhiteSpace(summoner.FormattedName))
        {
            DisplayName = !string.IsNullOrWhiteSpace(summoner.GameName)
                ? summoner.GameName
                : summoner.DisplayName;
            TagLine = summoner.TagLine;
            HasTagLine = !string.IsNullOrWhiteSpace(summoner.TagLine);
            Level = summoner.SummonerLevel;
            IsConnected = true;

            if (summoner.ProfileIconId > 0 && summoner.ProfileIconId != _currentIconId)
            {
                _currentIconId = summoner.ProfileIconId;
                TriggerAvatarLoad(summoner.ProfileIconId);
            }
        }
        else
        {
            DisplayName = _coordinator.Detector.Status == ClientConnectionStatus.Connected
                ? "获取中..."
                : "未连接";
            TagLine = string.Empty;
            HasTagLine = false;
            Level = 0;
            IsConnected = _coordinator.Detector.Status == ClientConnectionStatus.Connected;
            ClearAvatar();
        }
    }

    private void UpdateStatus(ClientConnectionStatus status)
    {
        IsConnected = status == ClientConnectionStatus.Connected;
        if (!IsConnected)
        {
            DisplayName = "未连接";
            TagLine = string.Empty;
            HasTagLine = false;
            Level = 0;
            ClearAvatar();
        }
    }

    private void TriggerAvatarLoad(int iconId)
    {
        _avatarCts?.Cancel();
        _avatarCts?.Dispose();
        _avatarCts = new CancellationTokenSource();
        var ct = _avatarCts.Token;

        _ = LoadAvatarAsync(iconId, ct);
    }

    private async Task LoadAvatarAsync(int iconId, CancellationToken ct)
    {
        try
        {
            var bytes = await _coordinator.StateCoordinator.RestClient
                .GetByteArrayAsync($"/lol-game-data/assets/v1/profile-icons/{iconId}.jpg", ct)
                .ConfigureAwait(false);

            if (bytes is { Length: > 0 } && !ct.IsCancellationRequested)
            {
                using var ms = new MemoryStream(bytes);
                var bitmap = Bitmap.DecodeToWidth(ms, 80, BitmapInterpolationMode.MediumQuality);

                Dispatcher.UIThread.Post(() =>
                {
                    if (ct.IsCancellationRequested || _disposed)
                    {
                        bitmap.Dispose();
                        return;
                    }

                    var oldBitmap = AvatarBitmap;
                    AvatarBitmap = bitmap;
                    HasAvatar = true;
                    if (oldBitmap is not null)
                        Dispatcher.UIThread.Post(() => oldBitmap.Dispose(), DispatcherPriority.Background);
                });
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"Failed to load profile avatar: {ex.Message}");
        }
    }

    private void ClearAvatar()
    {
        _currentIconId = -1;
        _avatarCts?.Cancel();
        _avatarCts?.Dispose();
        _avatarCts = null;

        var oldBitmap = AvatarBitmap;
        AvatarBitmap = null;
        HasAvatar = false;
        if (oldBitmap is not null)
            Dispatcher.UIThread.Post(() => oldBitmap.Dispose(), DispatcherPriority.Background);
    }
}