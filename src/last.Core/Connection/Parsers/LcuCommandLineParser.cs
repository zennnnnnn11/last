using System.Text.RegularExpressions;
using last.Core.Connection.Models;

namespace last.Core.Connection.Parsers;

public static partial class LcuCommandLineParser
{
    [GeneratedRegex(@"--app-port=(\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex PortRegex();

    [GeneratedRegex(@"--remoting-auth-token=([\w-_]+)", RegexOptions.CultureInvariant)]
    private static partial Regex AuthTokenRegex();

    [GeneratedRegex(@"--app-pid=(\d+)", RegexOptions.CultureInvariant)]
    private static partial Regex PidRegex();

    [GeneratedRegex(@"--rso[_-]platform[_-]id=([\w-_]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PlatformIdRegex();

    [GeneratedRegex(@"--region=([\w-_]+)", RegexOptions.CultureInvariant)]
    private static partial Regex RegionRegex();

    public static LcuCredentials? Parse(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
            return null;

        var portMatch = PortRegex().Match(commandLine);
        var authMatch = AuthTokenRegex().Match(commandLine);
        var pidMatch = PidRegex().Match(commandLine);

        if (!portMatch.Success || !authMatch.Success || !pidMatch.Success)
            return null;

        if (!int.TryParse(portMatch.Groups[1].ValueSpan, out var port) ||
            !int.TryParse(pidMatch.Groups[1].ValueSpan, out var pid))
            return null;

        var token = authMatch.Groups[1].Value;

        var platformMatch = PlatformIdRegex().Match(commandLine);
        var platformId = platformMatch.Success ? platformMatch.Groups[1].Value : string.Empty;

        var regionMatch = RegionRegex().Match(commandLine);
        var region = regionMatch.Success ? regionMatch.Groups[1].Value : string.Empty;

        return new LcuCredentials(
            port,
            token,
            pid,
            platformId,
            region);
    }
}