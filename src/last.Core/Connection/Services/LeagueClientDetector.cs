using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using last.Core.Connection.Http;
using last.Core.Connection.Models;
using last.Core.Connection.Native;
using last.Core.Connection.Parsers;
using Microsoft.Win32.SafeHandles;

namespace last.Core.Connection.Services;

public sealed class LeagueClientDetector : ILeagueClientDetector
{
    public const string TargetProcessName = "LeagueClientUx";

    public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan IdlePollInterval = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan LongPollInterval = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan AccessDeniedPollInterval = TimeSpan.FromSeconds(10);

    private static readonly HttpMessageInvoker SafeHttpInvoker = new(LcuHttpClientFactory.CreateSafeHandler(), true);

    private readonly IProcessCommandLineReader _commandLineReader;
    private readonly ILcuHandshakeProber _prober;
    private readonly Func<IReadOnlyList<int>> _processEnumerator;
    private readonly SemaphoreSlim _scanGate = new(1, 1);
    private CancellationTokenSource? _cts;

    private int _disconnectedConsecutiveScans;
    private int _noCommandLineCount;
    private Task? _pollLoopTask;
    private AutoResetEvent? _processWaitEvent;
    private RegisteredWaitHandle? _registeredWaitHandle;

    private PeriodicTimer? _timer;

    private SafeProcessHandle? _watchedProcessHandle;

    public LeagueClientDetector(
        IProcessCommandLineReader? commandLineReader = null,
        ILcuHandshakeProber? prober = null,
        Func<IReadOnlyList<int>>? processEnumerator = null)
    {
        _commandLineReader = commandLineReader ?? WindowsProcessCommandLine.Instance;
        _prober = prober ?? LcuHandshakeProber.Instance;
        _processEnumerator = processEnumerator ?? DefaultEnumeratePids;

        LastDiagnosticInfo = new ProcessScanDiagnosticInfo(
            ClientConnectionStatus.Disconnected,
            [],
            null,
            DateTimeOffset.UtcNow);
    }

    public ClientConnectionStatus Status { get; private set; } = ClientConnectionStatus.Disconnected;
    public LcuCredentials? CurrentCredentials { get; private set; }
    public ProcessScanDiagnosticInfo LastDiagnosticInfo { get; private set; }
    public bool IsRunning => _pollLoopTask is not null && !(_cts?.IsCancellationRequested ?? true);
    public bool IsManuallyDisconnected { get; private set; }

    public event Action<LcuCredentials?>? CredentialsChanged;
    public event Action<ClientConnectionStatus>? StatusChanged;
    public event Action<ProcessScanDiagnosticInfo>? DiagnosticUpdated;

    public void Start()
    {
        if (IsRunning)
            return;

        _cts = new CancellationTokenSource();
        _timer = new PeriodicTimer(DefaultPollInterval);
        _pollLoopTask = Task.Run(() => RunPollLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        var cts = _cts;
        var timer = _timer;
        var task = _pollLoopTask;

        if (cts is null)
            return;

        _timer = null;
        _cts = null;
        _pollLoopTask = null;

        try
        {
            cts.Cancel();
        }
        catch
        {
        }

        timer?.Dispose();

        if (task is not null)
            _ = task.ContinueWith(
                _ => cts.Dispose(),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        else
            cts.Dispose();
    }

    public void DisconnectManually()
    {
        IsManuallyDisconnected = true;
        UnwatchProcessExit();
        if (Status == ClientConnectionStatus.Connected || CurrentCredentials is not null)
        {
            CurrentCredentials = null;
            UpdateStatus(ClientConnectionStatus.Disconnected);
            CredentialsChanged?.Invoke(null);
        }

        AdjustTimerInterval(DefaultPollInterval);
    }

    public void ResumeAutoConnect()
    {
        IsManuallyDisconnected = false;
        AdjustTimerInterval(DefaultPollInterval);
    }

    public async Task<ProcessScanDiagnosticInfo> CheckNowAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _scanGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            return LastDiagnosticInfo;
        }
        catch (OperationCanceledException)
        {
            return LastDiagnosticInfo;
        }

        try
        {
            return await ExecuteScanAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                _scanGate.Release();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    public void Dispose()
    {
        Stop();
        UnwatchProcessExit();
        _scanGate.Dispose();
    }

    private static List<int> DefaultEnumeratePids()
    {
        try
        {
            using var processes = new ProcessListWrapper(Process.GetProcessesByName(TargetProcessName));
            return processes.Processes.Select(p => p.Id).ToList();
        }
        catch
        {
            return [];
        }
    }

    private void AdjustTimerInterval(TimeSpan interval)
    {
        if (_timer is not null)
            _timer.Period = interval;
    }

    private async Task RunPollLoopAsync(CancellationToken ct)
    {
        try
        {
            await CheckNowAsync(ct).ConfigureAwait(false);

            while (!ct.IsCancellationRequested && _timer is not null)
            {
                if (!await _timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
                    break;

                await CheckNowAsync(ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch
        {
        }
    }

    private async Task<ProcessScanDiagnosticInfo> ExecuteScanAsync(CancellationToken ct)
    {
        var pids = _processEnumerator();
        var now = DateTimeOffset.UtcNow;

        if (pids.Count == 0)
        {
            _noCommandLineCount = 0;
            _disconnectedConsecutiveScans++;
            UnwatchProcessExit();

            if (IsManuallyDisconnected)
                IsManuallyDisconnected = false;

            if (Status != ClientConnectionStatus.Disconnected || CurrentCredentials is not null)
            {
                CurrentCredentials = null;
                UpdateStatus(ClientConnectionStatus.Disconnected);
                CredentialsChanged?.Invoke(null);
            }

            // 当连续未发现客户端进程（空闲态挂机）时，自适应退避至 5 秒轮询，消除高频全进程枚举空转
            AdjustTimerInterval(_disconnectedConsecutiveScans >= 3 ? IdlePollInterval : DefaultPollInterval);

            var disconnectedInfo = new ProcessScanDiagnosticInfo(
                ClientConnectionStatus.Disconnected,
                pids,
                null,
                now);

            PublishDiagnostic(disconnectedInfo);
            return disconnectedInfo;
        }

        _disconnectedConsecutiveScans = 0;

        if (pids.Count > 1)
        {
            _noCommandLineCount = 0;
            UpdateStatus(ClientConnectionStatus.MultipleClientsDetected);

            var multiInfo = new ProcessScanDiagnosticInfo(
                ClientConnectionStatus.MultipleClientsDetected,
                pids,
                null,
                now,
                ErrorMessage: $"检测到 {pids.Count} 个客户端进程同时运行，已挂起自动连接以防混淆。");

            PublishDiagnostic(multiInfo);
            return multiInfo;
        }

        var targetPid = pids[0];
        var commandLine = _commandLineReader.GetCommandLine(targetPid);
        var parsed = LcuCommandLineParser.Parse(commandLine);

        // Lockfile 兜底闭环：若启动参数因提权隔离无法通过命令行获取，通过多源安装路径查找 lockfile
        if (parsed is null) parsed = LcuLockfileParser.TryFindCredentials(targetPid, _commandLineReader);

        if (parsed is null)
        {
            _noCommandLineCount++;
            var isAccessDenied = _noCommandLineCount >= 5;
            var status = isAccessDenied
                ? ClientConnectionStatus.AccessDenied
                : ClientConnectionStatus.Connecting;

            UpdateStatus(status);

            // AccessDenied 降频退避：连续 5 次失败进入 AccessDenied 状态后，降频为 10 秒轮询，消除无意义的 CPU/IO 空转
            if (isAccessDenied)
                AdjustTimerInterval(AccessDeniedPollInterval);

            var failureInfo = new ProcessScanDiagnosticInfo(
                status,
                pids,
                null,
                now,
                _noCommandLineCount,
                isAccessDenied
                    ? "检测到游戏客户端正在运行，但连续 5 次读取启动参数及 Lockfile 失败。可能需要以管理员身份运行本程序。"
                    : "正在等待客户端启动参数或 Lockfile 就绪...");

            PublishDiagnostic(failureInfo);
            return failureInfo;
        }

        _noCommandLineCount = 0;

        if (IsManuallyDisconnected)
        {
            var manualInfo = new ProcessScanDiagnosticInfo(
                ClientConnectionStatus.Disconnected,
                pids,
                null,
                now,
                ErrorMessage: "用户已手动断开连接。");

            PublishDiagnostic(manualInfo);
            return manualInfo;
        }

        if (Status == ClientConnectionStatus.Connected &&
            CurrentCredentials is not null &&
            CurrentCredentials.Pid == parsed.Pid &&
            CurrentCredentials.Port == parsed.Port &&
            CurrentCredentials.Token == parsed.Token)
        {
            var alreadyConnectedInfo = new ProcessScanDiagnosticInfo(
                ClientConnectionStatus.Connected,
                pids,
                CurrentCredentials,
                now);

            PublishDiagnostic(alreadyConnectedInfo);
            return alreadyConnectedInfo;
        }

        var probeSuccess = await _prober.ProbeAsync(parsed, ct).ConfigureAwait(false);
        if (!probeSuccess)
        {
            UpdateStatus(ClientConnectionStatus.Connecting);

            var probeFailedInfo = new ProcessScanDiagnosticInfo(
                ClientConnectionStatus.Connecting,
                pids,
                null,
                now,
                ErrorMessage: "凭据已解析，正在等待 LCU 本地 HTTP Web 服务端口监听响应...");

            PublishDiagnostic(probeFailedInfo);
            return probeFailedInfo;
        }

        // 回填平台 ID：LCU 本地 HTTP 端口已通过探针验证就绪，此时请求 /lol-rso-auth/v1/authorization 可 100% 稳定获取真实大区
        if (string.IsNullOrWhiteSpace(parsed.PlatformId))
        {
            var platformId = await TryResolvePlatformIdAsync(parsed, ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(platformId))
                parsed = parsed with { PlatformId = platformId };
        }

        CurrentCredentials = parsed;
        UpdateStatus(ClientConnectionStatus.Connected);
        CredentialsChanged?.Invoke(CurrentCredentials);

        WatchProcessExit(parsed.Pid);
        AdjustTimerInterval(LongPollInterval);

        var connectedInfo = new ProcessScanDiagnosticInfo(
            ClientConnectionStatus.Connected,
            pids,
            CurrentCredentials,
            now);

        PublishDiagnostic(connectedInfo);
        return connectedInfo;
    }

    private void WatchProcessExit(int pid)
    {
        UnwatchProcessExit();

        try
        {
            // 0x00101000 = SYNCHRONIZE (0x00100000) | PROCESS_QUERY_LIMITED_INFORMATION (0x1000)
            var handle = WindowsProcessCommandLine.OpenProcess(0x00101000, false, pid);
            if (handle.IsInvalid)
                return;

            _watchedProcessHandle = handle;
            var safeWaitHandle = new SafeWaitHandle(handle.DangerousGetHandle(), false);
            _processWaitEvent = new AutoResetEvent(false) { SafeWaitHandle = safeWaitHandle };
            _registeredWaitHandle = ThreadPool.RegisterWaitForSingleObject(
                _processWaitEvent,
                (state, timedOut) =>
                {
                    if (!timedOut)
                        // 客户端退出时系统内核即时发出信号，异步触发刷新断连状态并安全捕获异常
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await CheckNowAsync().ConfigureAwait(false);
                            }
                            catch
                            {
                            }
                        });
                },
                null,
                Timeout.Infinite,
                true);
        }
        catch
        {
        }
    }

    private static async Task<string?> TryResolvePlatformIdAsync(LcuCredentials creds, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{creds.BaseUrl}lol-rso-auth/v1/authorization");
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic", creds.BasicAuthHeaderValue);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(2));

            using var res = await SafeHttpInvoker.SendAsync(req, cts.Token).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
                return null;

            await using var stream = await res.Content.ReadAsStreamAsync(cts.Token).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token).ConfigureAwait(false);
            if (doc.RootElement.TryGetProperty("currentPlatformId", out var prop))
            {
                var val = prop.GetString();
                if (!string.IsNullOrWhiteSpace(val))
                    return val;
            }
        }
        catch
        {
        }

        return null;
    }

    private void UnwatchProcessExit()
    {
        try
        {
            _registeredWaitHandle?.Unregister(null);
        }
        catch
        {
        }

        _registeredWaitHandle = null;

        try
        {
            _processWaitEvent?.Dispose();
        }
        catch
        {
        }

        _processWaitEvent = null;

        try
        {
            _watchedProcessHandle?.Dispose();
        }
        catch
        {
        }

        _watchedProcessHandle = null;
    }

    private void UpdateStatus(ClientConnectionStatus newStatus)
    {
        if (Status != newStatus)
        {
            Status = newStatus;
            StatusChanged?.Invoke(Status);
        }
    }

    private void PublishDiagnostic(ProcessScanDiagnosticInfo info)
    {
        LastDiagnosticInfo = info;
        DiagnosticUpdated?.Invoke(info);
    }

    private sealed class ProcessListWrapper(Process[] processes) : IDisposable
    {
        public Process[] Processes { get; } = processes;

        public void Dispose()
        {
            foreach (var p in Processes)
                p.Dispose();
        }
    }
}