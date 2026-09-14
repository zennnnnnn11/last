using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace last.Services;

/// <summary>
///     轻量级生产就绪型本地文件日志服务（Native AOT 安全、线程安全、零外部依赖）。
///     确保在生产 Release 环境脱离调试器时，未捕获异常与关键诊断信息仍能可靠落盘。
/// </summary>
public static class AppLogger
{
    private static readonly Lock Gate = new();
    private static readonly string LogDirectory;
    private static readonly string CurrentLogFilePath;
    private static StreamWriter? _writer;
    private static bool _initialized;

    static AppLogger()
    {
        try
        {
            var baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "last",
                "logs");

            LogDirectory = baseDir;
            Directory.CreateDirectory(LogDirectory);

            CurrentLogFilePath = Path.Combine(
                LogDirectory,
                $"app-{DateTime.Now:yyyyMMdd}.log");

            var stream = new FileStream(
                CurrentLogFilePath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.ReadWrite);

            _writer = new StreamWriter(stream, Encoding.UTF8)
            {
                AutoFlush = true
            };

            _initialized = true;
        }
        catch (Exception ex)
        {
            Trace.WriteLine($"[AppLogger] Initialization failed: {ex.Message}");
            LogDirectory = string.Empty;
            CurrentLogFilePath = string.Empty;
        }
    }

    public static void Info(string message)
    {
        Write("INFO", message, null);
    }

    public static void Warn(string message)
    {
        Write("WARN", message, null);
    }

    public static void Error(string message, Exception? ex = null)
    {
        Write("ERROR", message, ex);
    }

    public static void Fatal(string message, Exception? ex = null)
    {
        Write("FATAL", message, ex);
    }

    private static void Write(string level, string message, Exception? ex)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var threadId = Environment.CurrentManagedThreadId;
        var logLine = $"[{timestamp}] [{level}] [T:{threadId:D2}] {message}";

        if (ex != null) logLine = $"{logLine}{Environment.NewLine}{ex}";

        // 镜像输出至 Trace，保障调试器附加时仍可实时捕获
        Trace.WriteLine(logLine);

        if (!_initialized || _writer == null)
            return;

        lock (Gate)
        {
            try
            {
                _writer.WriteLine(logLine);
            }
            catch
            {
                // 忽略日志文件并发写入故障，绝不影响主流程
            }
        }
    }

    public static void Flush()
    {
        lock (Gate)
        {
            try
            {
                _writer?.Flush();
            }
            catch
            {
            }
        }
    }

    public static void CloseAndFlush()
    {
        lock (Gate)
        {
            try
            {
                _writer?.Flush();
                _writer?.Dispose();
                _writer = null;
                _initialized = false;
            }
            catch
            {
                // 确保安全释放
            }
        }
    }
}