using System.Text;
using System.Text.Json;
using last.Core.Connection.Models;
using last.Core.Connection.Native;
using Microsoft.Win32;

namespace last.Core.Connection.Parsers;

public static class LcuLockfileParser
{
    private static readonly Lock DirectoryCacheLock = new();
    private static List<string>? _cachedStaticDirectories;
    private static DateTimeOffset _lastDirectoryScan = DateTimeOffset.MinValue;
    private static readonly TimeSpan DirectoryCacheTtl = TimeSpan.FromMinutes(2);

    public static LcuCredentials? Parse(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        // 格式为：ProcessName:PID:Port:AuthToken:Protocol
        var parts = content.Trim().Split(':');
        if (parts.Length < 5)
            return null;

        if (!int.TryParse(parts[1], out var pid) || !int.TryParse(parts[2], out var port))
            return null;

        var token = parts[3];
        if (string.IsNullOrEmpty(token))
            return null;

        return new LcuCredentials(
            port,
            token,
            pid,
            string.Empty,
            string.Empty);
    }

    public static LcuCredentials? TryReadFromDirectory(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return null;

        // 优先检查标准 lockfile，兜底检查 LeagueClientUx.lockfile
        string[] candidates = ["lockfile", "LeagueClientUx.lockfile"];
        foreach (var name in candidates)
        {
            var filePath = Path.Combine(directory, name);
            if (!File.Exists(filePath))
                continue;

            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(fs, Encoding.UTF8);
                var content = reader.ReadToEnd();
                var creds = Parse(content);
                if (creds is not null)
                    return creds;
            }
            catch
            {
                // 忽略被独占或临时不可读错误
            }
        }

        return null;
    }

    /// <summary>
    ///     多源发现 Lockfile 凭据：结合进程路径与带 TTL 内存缓存的安装目录列表进行极速闭环查找。
    /// </summary>
    public static LcuCredentials? TryFindCredentials(int expectedPid,
        IProcessCommandLineReader? commandLineReader = null)
    {
        // 1. 首选：若有指定 PID，尝试通过进程有限查询权限获取真实目录（零额外扫描）
        if (expectedPid > 0 && commandLineReader != null)
        {
            var exePath = commandLineReader.GetProcessExecutablePath(expectedPid);
            if (!string.IsNullOrWhiteSpace(exePath))
            {
                var dir = Path.GetDirectoryName(exePath);
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    var creds = TryReadFromDirectory(dir);
                    if (creds is not null && creds.Pid == expectedPid)
                        return creds;
                }
            }
        }

        // 2. 兜底：仅在内存缓存中的真实存在物理目录做极速 File.Exists
        var staticDirs = GetCachedStaticDirectories();
        for (var i = 0; i < staticDirs.Count; i++)
        {
            var dir = staticDirs[i];
            var creds = TryReadFromDirectory(dir);
            if (creds is null)
                continue;

            if (expectedPid <= 0 || creds.Pid == expectedPid)
                return creds;
        }

        return null;
    }

    /// <summary>
    ///     获取系统已探测到的物理存在客户端目录列表（2 分钟 TTL 内存缓存，避免轮询时重复读注册表与 JSON）。
    /// </summary>
    public static IReadOnlyList<string> GetCachedStaticDirectories()
    {
        var now = DateTimeOffset.UtcNow;
        if (_cachedStaticDirectories is not null && now - _lastDirectoryScan < DirectoryCacheTtl)
            return _cachedStaticDirectories;

        lock (DirectoryCacheLock)
        {
            if (_cachedStaticDirectories is not null && now - _lastDirectoryScan < DirectoryCacheTtl)
                return _cachedStaticDirectories;

            var discovered = DiscoverStaticExistingDirectories();
            _cachedStaticDirectories = discovered;
            _lastDirectoryScan = now;
            return discovered;
        }
    }

    private static List<string> DiscoverStaticExistingDirectories()
    {
        var validDirs = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddIfValid(string? dir)
        {
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir) && seen.Add(dir))
                validDirs.Add(dir);
        }

        // 1. Windows 注册表：国服 Tencent 键
        string[] tencentRegKeys =
        [
            @"HKEY_CURRENT_USER\Software\Tencent\LOL",
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Tencent\LOL",
            @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Tencent\LOL"
        ];
        foreach (var regKey in tencentRegKeys)
            if (TryGetRegistryString(regKey, "InstallPath", out var installPath) &&
                !string.IsNullOrWhiteSpace(installPath))
            {
                AddIfValid(Path.Combine(installPath, "LeagueClient"));
                AddIfValid(installPath);
            }

        // 2. Windows 注册表：外服 Riot 键
        string[] riotRegKeys =
        [
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Riot Games, Inc\League of Legends",
            @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Riot Games, Inc\League of Legends"
        ];
        foreach (var regKey in riotRegKeys)
            if (TryGetRegistryString(regKey, "Location", out var location) && !string.IsNullOrWhiteSpace(location))
                AddIfValid(location);

        // 3. %ProgramData%\Riot Games\RiotClientInstalls.json
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        if (!string.IsNullOrWhiteSpace(programData))
        {
            var jsonPath = Path.Combine(programData, "Riot Games", "RiotClientInstalls.json");
            if (File.Exists(jsonPath))
                foreach (var dir in ExtractPathsFromRiotClientInstallsJson(jsonPath))
                    AddIfValid(dir);
        }

        // 4. 常见默认盘符路径
        string[] commonPaths =
        [
            @"C:\Riot Games\League of Legends",
            @"D:\Riot Games\League of Legends",
            @"E:\Riot Games\League of Legends",
            @"C:\WeGameApps\英雄联盟\LeagueClient",
            @"D:\WeGameApps\英雄联盟\LeagueClient",
            @"E:\WeGameApps\英雄联盟\LeagueClient",
            @"C:\Program Files (x86)\Tencent\英雄联盟\LeagueClient",
            @"D:\Program Files (x86)\Tencent\英雄联盟\LeagueClient"
        ];
        foreach (var path in commonPaths)
            AddIfValid(path);

        return validDirs;
    }

    private static bool TryGetRegistryString(string fullKeyPath, string valueName, out string? value)
    {
        value = null;
        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            var raw = Registry.GetValue(fullKeyPath, valueName, null);
            if (raw is string s && !string.IsNullOrWhiteSpace(s))
            {
                value = s;
                return true;
            }
        }
        catch
        {
        }

        return false;
    }

    private static List<string> ExtractPathsFromRiotClientInstallsJson(string jsonPath)
    {
        var paths = new List<string>();
        try
        {
            var content = File.ReadAllText(jsonPath);
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("associated_client", out var assoc) &&
                assoc.ValueKind == JsonValueKind.Object)
                foreach (var prop in assoc.EnumerateObject())
                {
                    var normalized = prop.Name.Replace('/', '\\');
                    var full = Path.GetFullPath(normalized);
                    if (Directory.Exists(full))
                        paths.Add(full);
                }
        }
        catch
        {
        }

        return paths;
    }
}