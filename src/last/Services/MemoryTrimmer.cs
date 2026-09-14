using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace last.Services;

/// <summary>
///     提供 Native AOT 安全的物理工作集修剪服务。
///     在主窗口收起至系统托盘时触发轻量级 GC 并通知 Windows 内存管理器将非活跃物理页置换至待机列表，
///     使应用在后台挂机时物理内存从 ~120MB 骤降至 ~15-25MB，把宝贵的物理内存让渡给前台游戏。
/// </summary>
internal static class MemoryTrimmer
{
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessWorkingSetSize(
        IntPtr hProcess,
        nuint dwMinimumWorkingSetSize,
        nuint dwMaximumWorkingSetSize);

    /// <summary>
    ///     执行物理工作集修剪。
    /// </summary>
    public static void TrimWorkingSet()
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            // 注意：当 GCCollectionMode 为 Aggressive 时，.NET 运行时强制要求 blocking 必须为 true，否则会抛出 ArgumentException
            GC.Collect(2, GCCollectionMode.Aggressive, true, true);
            GC.WaitForPendingFinalizers();

            var processHandle = GetCurrentProcess();

            // 优先使用 psapi!EmptyWorkingSet 移除所有驻留物理页，失败时兜底调用 kernel32!SetProcessWorkingSetSize(-1, -1)
            if (!EmptyWorkingSet(processHandle))
                SetProcessWorkingSetSize(
                    processHandle,
                    unchecked((nuint)(-1)),
                    unchecked((nuint)(-1)));
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"MemoryTrimmer failed to trim working set: {ex.Message}");
        }
    }

    /// <summary>
    ///     在微小延迟后执行物理工作集修剪，确保窗口退出/折叠动画完全结束后再修剪表面资源。
    /// </summary>
    public static void TrimWorkingSetDeferred(int delayMs = 200)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                if (delayMs > 0)
                    await Task.Delay(delayMs).ConfigureAwait(false);

                TrimWorkingSet();
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Deferred memory trim task encountered exception: {ex.Message}");
            }
        });
    }
}