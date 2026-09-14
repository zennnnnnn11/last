using System;
using System.Threading;
using Avalonia.Threading;

namespace last.Services;

/// <summary>
///     应用程序单实例互斥保护与跨进程唤醒协调服务（依据微软官方 Mutex 与 EventWaitHandle 规范，Native AOT 安全）。
/// </summary>
public sealed class SingleInstanceManager : IDisposable
{
    private const string MutexName = @"Local\last_App_SingleInstance_Mutex_987A1E6C";
    private const string WakeUpEventName = @"Local\last_App_SingleInstance_WakeUp_987A1E6C";

    private static Action? _onWakeUp;
    private readonly ManualResetEvent _cancelEvent;
    private readonly Thread _listenerThread;

    private readonly Mutex _mutex;
    private readonly EventWaitHandle _wakeUpEvent;
    private bool _disposed;

    private SingleInstanceManager(Mutex mutex, EventWaitHandle wakeUpEvent)
    {
        _mutex = mutex;
        _wakeUpEvent = wakeUpEvent;
        _cancelEvent = new ManualResetEvent(false);

        _listenerThread = new Thread(ListenLoop)
        {
            IsBackground = true,
            Name = "SingleInstanceWakeUpListener"
        };
        _listenerThread.Start();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            _cancelEvent.Set();
            if (_listenerThread.IsAlive) _listenerThread.Join(500);
        }
        catch
        {
        }

        try
        {
            _mutex.ReleaseMutex();
        }
        catch
        {
        }

        _mutex.Dispose();
        _wakeUpEvent.Dispose();
        _cancelEvent.Dispose();
    }

    /// <summary>
    ///     尝试获取单实例互斥锁。若已有实例正在运行，唤醒已有实例并返回 false。
    /// </summary>
    public static bool TryAcquire(out SingleInstanceManager? manager)
    {
        manager = null;

        if (!OperatingSystem.IsWindows())
            return true;

        Mutex? mutex = null;
        EventWaitHandle? wakeUpEvent = null;

        try
        {
            mutex = new Mutex(true, MutexName, out var createdNew);
            if (!createdNew)
            {
                // 已有运行实例：发送唤醒信号唤醒主窗口，随后退出当前重复进程
                SignalExistingInstance();
                mutex.Dispose();
                return false;
            }

            wakeUpEvent = new EventWaitHandle(false, EventResetMode.AutoReset, WakeUpEventName, out _);
            manager = new SingleInstanceManager(mutex, wakeUpEvent);
            return true;
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"[SingleInstanceManager] Failed during mutex acquisition: {ex.Message}");
            mutex?.Dispose();
            wakeUpEvent?.Dispose();
            // 环境异常时兜底放行
            return true;
        }
    }

    /// <summary>
    ///     向已运行的实例发送拉起窗口的前台唤醒信号。
    /// </summary>
    public static void SignalExistingInstance()
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            if (EventWaitHandle.TryOpenExisting(WakeUpEventName, out var wakeUpEvent))
                using (wakeUpEvent)
                {
                    wakeUpEvent.Set();
                }
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"[SingleInstanceManager] Failed to signal existing instance: {ex.Message}");
        }
    }

    /// <summary>
    ///     设置或重置接收到重复启动时的唤醒动作。
    /// </summary>
    public static void SetWakeUpAction(Action? onWakeUp)
    {
        _onWakeUp = onWakeUp;
    }

    private void ListenLoop()
    {
        var waitHandles = new WaitHandle[] { _wakeUpEvent, _cancelEvent };

        while (!_disposed)
            try
            {
                var signaledIndex = WaitHandle.WaitAny(waitHandles);
                if (signaledIndex == 0) // _wakeUpEvent 触发
                {
                    if (_disposed)
                        break;

                    if (_onWakeUp is { } callback) Dispatcher.UIThread.Post(callback);
                }
                else
                {
                    break;
                }
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"[SingleInstanceManager] Wake-up listener loop exception: {ex.Message}");
            }
    }
}