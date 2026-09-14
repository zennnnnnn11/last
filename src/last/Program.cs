using System;
using System.Threading.Tasks;
using Avalonia;
using last.Services;

namespace last;

internal class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // 1. 拦截非 UI 线程中未观察到的后台任务异常，持久化日志并避免任务终结时进程直接崩溃
        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            AppLogger.Error("Unobserved background task exception", e.Exception);
            e.SetObserved();
        };

        // 2. 捕获应用域全局未捕获异常（此事件为 informational only，记录后进程将终止）
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            AppLogger.Fatal($"Unhandled AppDomain exception (IsTerminating: {e.IsTerminating})", ex);
            AppLogger.Flush();
        };

        // 3. 单实例互斥保护：检查是否已有运行实例；若有则向其发送前台唤醒信号并退出
        if (!SingleInstanceManager.TryAcquire(out var singleInstanceGuard))
        {
            AppLogger.Info("Another instance of last is already running. Signaled existing instance and exiting.");
            return;
        }

        using (singleInstanceGuard)
        {
            try
            {
                AppLogger.Info("Application initializing Avalonia runtime...");
                BuildAvaloniaApp()
                    .StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
                AppLogger.Fatal("Fatal application crash in Main entry point", ex);
            }
            finally
            {
                AppLogger.CloseAndFlush();
            }
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new SkiaOptions
            {
                MaxGpuResourceSizeBytes = 24 * 1024 * 1024
            })
#if DEBUG
            .WithDeveloperTools()
#endif
            .LogToTrace();
}