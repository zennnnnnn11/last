using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace last.Helpers;

/// <summary>
///     高性能 UI 操作合并与防抖节流器（领先执行 + 拖尾合并）
///     在短时间内高频触发时，首个事件立即执行确保 UI 零延迟响应；后续高频突发事件在窗口内合并为最后一次执行，杜绝 UI 线程洪峰。
/// </summary>
public sealed class CoalescingAction : IDisposable
{
    private readonly Action _action;
    private readonly Lock _gate = new();
    private readonly DispatcherPriority _priority;
    private readonly TimeSpan _window;
    private CancellationTokenSource? _cts;
    private bool _disposed;
    private bool _hasPending;
    private bool _inFlight;

    public CoalescingAction(Action action, TimeSpan? window = null, DispatcherPriority? priority = null)
    {
        _action = action ?? throw new ArgumentNullException(nameof(action));
        _window = window ?? TimeSpan.FromMilliseconds(100);
        _priority = priority ?? DispatcherPriority.Normal;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            _disposed = true;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }

    public void Invoke()
    {
        lock (_gate)
        {
            if (_disposed)
                return;

            if (!_inFlight)
            {
                // 领先边缘：空闲时的首次触发立即上屏，确保感知零延迟
                _inFlight = true;
                _hasPending = false;
                PostToDispatcher();

                _cts = new CancellationTokenSource();
                _ = CoolDownAsync(_cts);
            }
            else
            {
                // 节流窗口期内：标记有待执行的更新，冷却结束后拖尾执行最新值
                _hasPending = true;
            }
        }
    }

    private void PostToDispatcher()
    {
        if (Dispatcher.UIThread.CheckAccess())
            _action();
        else
            Dispatcher.UIThread.Post(_action, _priority);
    }

    private async Task CoolDownAsync(CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(_window, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        lock (_gate)
        {
            if (_disposed || cts.IsCancellationRequested)
                return;

            if (_hasPending)
            {
                _hasPending = false;
                PostToDispatcher();

                _cts = new CancellationTokenSource();
                _ = CoolDownAsync(_cts);
            }
            else
            {
                _inFlight = false;
                _cts = null;
            }
        }
    }
}