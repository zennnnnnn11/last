namespace last.Core.MatchHistory.Models;

public static class TencentSgpServerConfig
{
    public const string DefaultPlatformId = "HN10";
    public const string DefaultGatewayUrl = "https://hn10-k8s-sgp.lol.qq.com:21019";

    private static readonly Dictionary<string, string> Gateways = new(StringComparer.OrdinalIgnoreCase)
    {
        ["HN1"] = "https://hn1-k8s-sgp.lol.qq.com:21019",
        ["HN10"] = "https://hn10-k8s-sgp.lol.qq.com:21019",
        ["BGP2"] = "https://bgp2-k8s-sgp.lol.qq.com:21019",
        ["NJ100"] = "https://nj100-sgp.lol.qq.com:21019",
        ["GZ100"] = "https://gz100-sgp.lol.qq.com:21019",
        ["CQ100"] = "https://cq100-sgp.lol.qq.com:21019",
        ["TJ100"] = "https://tj100-sgp.lol.qq.com:21019",
        ["TJ101"] = "https://tj101-sgp.lol.qq.com:21019",
        ["PBE"] = "https://pbe-sgp.lol.qq.com:21019",
        ["PREPBE"] = "https://prepbe-sgp.lol.qq.com:21019"
    };

    private static readonly Dictionary<string, string> LegacyClusterMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        ["HN2"] = "NJ100",
        ["HN5"] = "NJ100",
        ["HN7"] = "NJ100",
        ["EDU1"] = "NJ100",
        ["BGP1"] = "NJ100",
        ["HN14"] = "NJ100",
        ["HN14_NEW"] = "NJ100",
        ["HN15"] = "NJ100",
        ["HN16"] = "NJ100",
        ["HN16_NEW"] = "NJ100",
        ["HN3"] = "GZ100",
        ["HN6"] = "GZ100",
        ["HN8"] = "GZ100",
        ["HN11"] = "GZ100",
        ["HN17"] = "GZ100",
        ["HN18"] = "GZ100",
        ["HN18_NEW"] = "GZ100",
        ["HN4"] = "CQ100",
        ["HN4_NEW"] = "CQ100",
        ["HN9"] = "CQ100",
        ["HN12"] = "CQ100",
        ["HN13"] = "CQ100",
        ["HN19"] = "CQ100",
        ["WT1"] = "TJ100",
        ["WT1_NEW"] = "TJ100",
        ["WT3"] = "TJ100",
        ["WT3_NEW"] = "TJ100",
        ["WT6"] = "TJ100",
        ["WT2"] = "TJ101",
        ["WT2_NEW"] = "TJ101",
        ["WT4"] = "TJ101",
        ["WT4_NEW"] = "TJ101",
        ["WT5"] = "TJ101",
        ["WT7"] = "TJ101"
    };

    public static string NormalizePlatformId(string? platformId)
    {
        if (string.IsNullOrWhiteSpace(platformId))
            return DefaultPlatformId;

        var normalized = platformId.Trim().ToUpperInvariant();
        if (normalized.StartsWith("TENCENT_", StringComparison.OrdinalIgnoreCase))
            normalized = normalized["TENCENT_".Length..];

        if (Gateways.ContainsKey(normalized))
            return normalized;

        return LegacyClusterMapping.GetValueOrDefault(normalized, DefaultPlatformId);
    }

    public static string GetGatewayUrl(string? platformId)
    {
        var normalized = NormalizePlatformId(platformId);
        return Gateways.GetValueOrDefault(normalized, DefaultGatewayUrl);
    }

    public static bool IsSupportedPlatform(string? platformId)
    {
        if (string.IsNullOrWhiteSpace(platformId))
            return false;

        var normalized = platformId.Trim().ToUpperInvariant();
        if (normalized.StartsWith("TENCENT_", StringComparison.OrdinalIgnoreCase))
            normalized = normalized["TENCENT_".Length..];

        return Gateways.ContainsKey(normalized) || LegacyClusterMapping.ContainsKey(normalized);
    }
}