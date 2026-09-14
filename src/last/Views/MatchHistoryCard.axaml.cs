using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using last.Helpers;
using last.ViewModels;

namespace last.Views;

public partial class MatchHistoryCard : UserControl
{
    private ScrollViewer? _scrollViewer;
    private MatchHistoryCardViewModel? _subscribedVm;

    public MatchHistoryCard()
    {
        InitializeComponent();

        MatchListBox.Loaded += OnListBoxLoaded;
        MatchListBox.AddHandler(
            ScrollViewer.ScrollChangedEvent,
            OnMatchListScrollChanged,
            RoutingStrategies.Bubble);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        MatchListBox.Loaded -= OnListBoxLoaded;
        MatchListBox.RemoveHandler(ScrollViewer.ScrollChangedEvent, OnMatchListScrollChanged);

        if (_scrollViewer != null)
        {
            _scrollViewer.PropertyChanged -= OnScrollViewerPropertyChanged;
            _scrollViewer = null;
        }

        if (_subscribedVm != null)
        {
            _subscribedVm.RequestScrollToTop -= OnRequestScrollToTop;
            _subscribedVm = null;
        }
    }

    private void OnListBoxLoaded(object? sender, RoutedEventArgs e)
    {
        EnsureScrollViewerAttached();
    }

    private void EnsureScrollViewerAttached()
    {
        if (_scrollViewer != null) return;
        var sv = MatchListBox.FindDescendantOfType<ScrollViewer>();
        if (sv == null) return;

        _scrollViewer = sv;
        sv.PropertyChanged += OnScrollViewerPropertyChanged;
        SmoothScrollViewerHelper.SetIsSmoothScrollEnabled(sv, true);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_subscribedVm != null)
        {
            _subscribedVm.RequestScrollToTop -= OnRequestScrollToTop;
            _subscribedVm = null;
        }

        if (DataContext is MatchHistoryCardViewModel vm)
        {
            _subscribedVm = vm;
            vm.RequestScrollToTop += OnRequestScrollToTop;
        }
    }

    private void OnRequestScrollToTop()
    {
        EnsureScrollViewerAttached();
        if (_scrollViewer != null)
            _scrollViewer.Offset = Vector.Zero;
        else if (MatchListBox.ItemCount > 0) MatchListBox.ScrollIntoView(0);
    }

    private void OnScrollViewerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == ScrollViewer.OffsetProperty || e.Property == ScrollViewer.ExtentProperty)
            CheckAndTriggerLoadMore();
    }

    private void OnMatchListScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        EnsureScrollViewerAttached();
        CheckAndTriggerLoadMore();
    }

    private void CheckAndTriggerLoadMore()
    {
        EnsureScrollViewerAttached();
        var sv = _scrollViewer;
        if (sv == null) return;

        if (sv.Extent.Height <= sv.Viewport.Height) return;

        var maxScroll = Math.Max(0, sv.Extent.Height - sv.Viewport.Height);
        var remaining = maxScroll - sv.Offset.Y;
        var threshold = sv.Extent.Height > 100 ? 220.0 : 4.0;

        if (remaining < threshold)
            if (DataContext is MatchHistoryCardViewModel vm && !vm.IsLoading && !vm.IsLoadingMore && vm.HasMoreMatches)
                _ = vm.LoadMoreAsync();
    }
}