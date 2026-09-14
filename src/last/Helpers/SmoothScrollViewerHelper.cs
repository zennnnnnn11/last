using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace last.Helpers;

/// <summary>
///     为 ScrollViewer 提供丝滑阻尼滚动扩展。
///     结合 Avalonia 官方 IsScrollInertiaEnabled（手势惯性）与本扩展的鼠标滚轮平滑插值（Wheel Interpolation），
///     彻底解决机械鼠标滚轮 50px 硬阶跃跳跃问题，实现媲美现代浏览器的阻尼流动感。
/// </summary>
public static class SmoothScrollViewerHelper
{
    public static readonly AttachedProperty<bool> IsSmoothScrollEnabledProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, bool>(
            "IsSmoothScrollEnabled",
            typeof(SmoothScrollViewerHelper));

    private static readonly AttachedProperty<SmoothScrollState?> StateProperty =
        AvaloniaProperty.RegisterAttached<ScrollViewer, SmoothScrollState?>(
            "State",
            typeof(SmoothScrollViewerHelper));

    static SmoothScrollViewerHelper()
    {
        IsSmoothScrollEnabledProperty.Changed.AddClassHandler<ScrollViewer>((sv, e) =>
        {
            if (e.NewValue is true)
            {
                var state = new SmoothScrollState(sv);
                sv.SetValue(StateProperty, state);
                sv.AddHandler(InputElement.PointerWheelChangedEvent, state.OnPointerWheelChanged,
                    RoutingStrategies.Tunnel);
            }
            else
            {
                var state = sv.GetValue(StateProperty);
                if (state != null)
                {
                    sv.RemoveHandler(InputElement.PointerWheelChangedEvent, state.OnPointerWheelChanged);
                    state.Dispose();
                    sv.SetValue(StateProperty, null);
                }
            }
        });
    }

    public static bool GetIsSmoothScrollEnabled(ScrollViewer element)
    {
        return element.GetValue(IsSmoothScrollEnabledProperty);
    }

    public static void SetIsSmoothScrollEnabled(ScrollViewer element, bool value)
    {
        element.SetValue(IsSmoothScrollEnabledProperty, value);
    }

    private sealed class SmoothScrollState : IDisposable
    {
        private readonly ScrollViewer _scrollViewer;
        private bool _isAnimating;
        private double _targetY;

        public SmoothScrollState(ScrollViewer scrollViewer)
        {
            _scrollViewer = scrollViewer;
            _targetY = scrollViewer.Offset.Y;

            // 当用户手动拖拽滚动条 Thumb 或外部重置时，同步更新目标值
            _scrollViewer.PropertyChanged += OnScrollViewerPropertyChanged;
        }

        public void Dispose()
        {
            _isAnimating = false;
            _scrollViewer.PropertyChanged -= OnScrollViewerPropertyChanged;
        }

        private void OnScrollViewerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == ScrollViewer.OffsetProperty && !_isAnimating)
                if (e.NewValue is Vector v)
                    _targetY = v.Y;
        }

        public void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            // 仅对有垂直位移的滚轮事件做插值
            if (Math.Abs(e.Delta.Y) < double.Epsilon) return;

            var extentHeight = _scrollViewer.Extent.Height;
            var viewportHeight = _scrollViewer.Viewport.Height;
            var maxScroll = Math.Max(0, extentHeight - viewportHeight);
            if (maxScroll <= 0) return;

            // 自动识别物理像素滚动或逻辑条目滚动：当 Extent 高度较小且接近整数时适配单行步长
            var isLogical = extentHeight > 0 && extentHeight < 300 &&
                            Math.Abs(extentHeight - Math.Round(extentHeight)) < 0.05;
            var stepSize = isLogical ? 1.0 : 65.0;
            var delta = -e.Delta.Y * stepSize;

            if (!_isAnimating) _targetY = _scrollViewer.Offset.Y;

            _targetY = Math.Clamp(_targetY + delta, 0, maxScroll);
            e.Handled = true;

            if (!_isAnimating)
            {
                _isAnimating = true;
                AnimateNextFrame();
            }
        }

        private void AnimateNextFrame()
        {
            var topLevel = TopLevel.GetTopLevel(_scrollViewer);
            if (topLevel != null)
                topLevel.RequestAnimationFrame(OnFrame);
            else
                Dispatcher.UIThread.Post(() => OnFrame(TimeSpan.Zero));
        }

        private void OnFrame(TimeSpan time)
        {
            if (!_isAnimating) return;

            var current = _scrollViewer.Offset.Y;
            var extentHeight = _scrollViewer.Extent.Height;
            var viewportHeight = _scrollViewer.Viewport.Height;
            var maxScroll = Math.Max(0, extentHeight - viewportHeight);
            _targetY = Math.Clamp(_targetY, 0, maxScroll);

            var isLogical = extentHeight > 0 && extentHeight < 300 &&
                            Math.Abs(extentHeight - Math.Round(extentHeight)) < 0.05;
            var minDiff = isLogical ? 0.04 : 0.4;
            var diff = _targetY - current;

            if (Math.Abs(diff) < minDiff)
            {
                _scrollViewer.Offset = new Vector(_scrollViewer.Offset.X, _targetY);
                _isAnimating = false;
                return;
            }

            // 指数阻尼插值算法（约 20% 每帧衰减），与高刷屏幕（60Hz/120Hz/144Hz）同步
            var step = diff * 0.20;
            var minStep = isLogical ? 0.04 : 0.25;
            if (Math.Abs(step) < minStep) step = Math.Sign(diff) * minStep;

            var nextY = current + step;
            _scrollViewer.Offset = new Vector(_scrollViewer.Offset.X, nextY);

            AnimateNextFrame();
        }
    }
}