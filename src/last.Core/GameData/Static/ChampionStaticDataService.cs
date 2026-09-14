using System.Globalization;
using last.Core.State.Api;
using last.Core.State.Models;

namespace last.Core.GameData.Static;

public sealed class ChampionStaticDataService : IChampionStaticDataService
{
    private static readonly Dictionary<int, string> KnownChampions = new()
    {
        [1] = "安妮",
        [2] = "奥拉夫",
        [3] = "加里奥",
        [4] = "崔斯特",
        [5] = "赵信",
        [6] = "厄加特",
        [7] = "乐芙兰",
        [8] = "弗拉基米尔",
        [9] = "费德提克",
        [10] = "凯尔",
        [11] = "易",
        [12] = "阿利斯塔",
        [13] = "瑞兹",
        [14] = "赛恩",
        [15] = "希维尔",
        [16] = "索拉卡",
        [17] = "提莫",
        [18] = "崔丝塔娜",
        [19] = "沃里克",
        [20] = "努努和威朗普",
        [21] = "厄运小姐",
        [22] = "艾希",
        [23] = "泰达米尔",
        [24] = "贾克斯",
        [25] = "莫甘娜",
        [26] = "基兰",
        [27] = "辛吉德",
        [28] = "伊芙琳",
        [29] = "图奇",
        [30] = "卡尔萨斯",
        [31] = "科加斯",
        [32] = "阿木木",
        [33] = "拉莫斯",
        [34] = "艾尼维亚",
        [35] = "萨科",
        [36] = "蒙多医生",
        [37] = "娑娜",
        [38] = "卡萨丁",
        [39] = "艾瑞莉娅",
        [40] = "迦娜",
        [41] = "普朗克",
        [42] = "库奇",
        [43] = "卡尔玛",
        [44] = "塔里克",
        [45] = "维迦",
        [48] = "特朗德尔",
        [50] = "斯维因",
        [51] = "凯特琳",
        [53] = "布里茨",
        [54] = "墨菲特",
        [55] = "卡特琳娜",
        [56] = "魔腾",
        [57] = "茂凯",
        [58] = "雷克顿",
        [59] = "嘉文四世",
        [60] = "伊莉丝",
        [61] = "奥莉安娜",
        [62] = "孙悟空",
        [63] = "布兰德",
        [64] = "李青",
        [67] = "薇恩",
        [68] = "兰博",
        [69] = "卡西奥佩娅",
        [72] = "斯卡纳",
        [74] = "黑默丁格",
        [75] = "内瑟斯",
        [76] = "奈德丽",
        [77] = "乌迪尔",
        [78] = "波比",
        [79] = "古拉加斯",
        [80] = "潘森",
        [81] = "伊泽瑞尔",
        [82] = "莫德凯撒",
        [83] = "约里克",
        [84] = "阿卡丽",
        [85] = "凯南",
        [86] = "盖伦",
        [89] = "蕾欧娜",
        [90] = "马尔扎哈",
        [91] = "泰隆",
        [92] = "锐雯",
        [96] = "克格莫",
        [98] = "慎",
        [99] = "拉克丝",
        [101] = "泽拉斯",
        [102] = "希瓦娜",
        [103] = "阿狸",
        [104] = "格雷福斯",
        [105] = "菲兹",
        [106] = "沃利贝尔",
        [107] = "雷恩加尔",
        [110] = "韦鲁斯",
        [111] = "诺提勒斯",
        [112] = "维克托",
        [113] = "瑟庄妮",
        [114] = "菲奥娜",
        [115] = "吉格斯",
        [117] = "璐璐",
        [119] = "德莱文",
        [120] = "赫卡里姆",
        [121] = "卡兹克",
        [122] = "达瑞斯",
        [126] = "杰斯",
        [127] = "丽桑卓",
        [131] = "黛安娜",
        [133] = "奎因",
        [134] = "辛德拉",
        [136] = "奥瑞利安·索尔",
        [141] = "凯隐",
        [142] = "佐伊",
        [143] = "婕拉",
        [145] = "卡莎",
        [147] = "萨拉芬妮",
        [150] = "纳尔",
        [154] = "扎克",
        [157] = "亚索",
        [161] = "维克兹",
        [163] = "塔莉垭",
        [164] = "卡蜜尔",
        [166] = "阿克尚",
        [200] = "卑尔维斯",
        [201] = "布隆",
        [202] = "烬",
        [203] = "千珏",
        [221] = "泽丽",
        [222] = "金克丝",
        [223] = "塔姆·肯奇",
        [234] = "佛耶戈",
        [235] = "赛娜",
        [236] = "卢锡安",
        [238] = "劫",
        [240] = "克烈",
        [245] = "艾克",
        [246] = "奇亚娜",
        [254] = "蔚",
        [266] = "亚托克斯",
        [267] = "娜美",
        [268] = "阿兹尔",
        [350] = "悠米",
        [360] = "莎弥拉",
        [412] = "锤石",
        [420] = "俄洛伊",
        [421] = "雷克塞",
        [427] = "艾翁",
        [429] = "卡莉丝塔",
        [432] = "巴德",
        [497] = "洛",
        [498] = "霞",
        [516] = "奥恩",
        [517] = "塞拉斯",
        [518] = "妮蔻",
        [523] = "厄斐琉斯",
        [526] = "芮尔",
        [555] = "派克",
        [711] = "薇古丝",
        [777] = "永恩",
        [875] = "瑟提",
        [876] = "莉莉娅",
        [887] = "格温",
        [888] = "烈娜塔 · 戈拉斯克",
        [893] = "欧罗拉",
        [895] = "尼菈",
        [897] = "奎桑提",
        [901] = "斯莫德",
        [902] = "米利欧",
        [910] = "贝蕾亚",
        [950] = "彗",
        [799] = "安蓓萨"
    };

    private readonly IGameDataApi? _gameDataApi;
    private readonly IGtimgHeroClient? _gtimgClient;
    private readonly Lock _lock = new();

    private Dictionary<int, ChampionStaticInfo> _champions = [];
    private int _isInitializing;

    public ChampionStaticDataService(
        IGtimgHeroClient? gtimgClient = null,
        IGameDataApi? gameDataApi = null)
    {
        _gtimgClient = gtimgClient;
        _gameDataApi = gameDataApi;
    }

    public ChampionStaticInfo? GetChampion(int championId)
    {
        lock (_lock)
        {
            return _champions.GetValueOrDefault(championId);
        }
    }

    public string GetIconUri(int championId)
    {
        lock (_lock)
        {
            if (_champions.TryGetValue(championId, out var champ))
            {
                if (!string.IsNullOrWhiteSpace(champ.SquarePortraitPath)) return champ.SquarePortraitPath;

                if (!string.IsNullOrWhiteSpace(champ.Alias))
                    return $"https://game.gtimg.cn/images/lol/act/img/champion/{champ.Alias}.png";
            }

            return $"/lol-game-data/assets/v1/champion-icons/{championId}.png";
        }
    }

    public IReadOnlyList<ChampionStaticInfo> GetAllChampions()
    {
        lock (_lock)
        {
            return _champions.Values.OrderBy(c => c.Id).ToList();
        }
    }

    public IReadOnlyList<ChampionStaticInfo> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];

        var trimmed = query.Trim();
        var isNumeric = int.TryParse(trimmed, CultureInfo.InvariantCulture, out var numericId);

        IReadOnlyList<ChampionStaticInfo> snapshot;
        lock (_lock)
        {
            snapshot = _champions.Values.ToList();
        }

        var results = new List<(ChampionStaticInfo Champ, int Score)>();

        foreach (var champ in snapshot)
        {
            var score = CalculateMatchScore(champ, trimmed, isNumeric, numericId);
            if (score > 0) results.Add((champ, score));
        }

        return results
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.Champ.Id)
            .Select(r => r.Champ)
            .ToList();
    }

    public string GetChampionName(int championId)
    {
        var shouldInit = false;
        lock (_lock)
        {
            if (_champions.TryGetValue(championId, out var champ) && !string.IsNullOrWhiteSpace(champ.Name))
                return champ.Name;

            if (KnownChampions.TryGetValue(championId, out var knownName))
                return knownName;

            if (_champions.Count == 0 && Volatile.Read(ref _isInitializing) == 0)
                shouldInit = true;
        }

        if (shouldInit)
            _ = InitializeAsync();

        return championId > 0 ? $"英雄 {championId}" : string.Empty;
    }

    public void UpdateFromLcu(IReadOnlyList<ChampionSimple> lcuChampions)
    {
        ArgumentNullException.ThrowIfNull(lcuChampions);

        lock (_lock)
        {
            var updated = new Dictionary<int, ChampionStaticInfo>(_champions);

            foreach (var lcu in lcuChampions)
            {
                if (lcu.Id <= 0)
                    continue;

                if (updated.TryGetValue(lcu.Id, out var existing))
                    updated[lcu.Id] = existing with
                    {
                        Name = string.IsNullOrWhiteSpace(existing.Name) ? lcu.Name : existing.Name,
                        Alias = string.IsNullOrWhiteSpace(existing.Alias) ? lcu.Alias : existing.Alias,
                        SquarePortraitPath = string.IsNullOrWhiteSpace(lcu.SquarePortraitPath)
                            ? existing.SquarePortraitPath
                            : lcu.SquarePortraitPath,
                        Roles = lcu.Roles is { Count: > 0 } ? lcu.Roles : existing.Roles
                    };
                else
                    updated[lcu.Id] = new ChampionStaticInfo(
                        lcu.Id,
                        lcu.Name,
                        lcu.Alias,
                        "",
                        lcu.Roles ?? [],
                        lcu.SquarePortraitPath);
            }

            _champions = updated;
        }
    }

    public void UpdateFromGtimg(GtimgHeroList gtimgData)
    {
        ArgumentNullException.ThrowIfNull(gtimgData);

        if (gtimgData.Hero is null or { Count: 0 })
            return;

        lock (_lock)
        {
            var updated = new Dictionary<int, ChampionStaticInfo>(_champions);

            foreach (var hero in gtimgData.Hero)
            {
                var id = hero.NumericHeroId;
                if (id <= 0)
                    continue;

                if (updated.TryGetValue(id, out var existing))
                    updated[id] = existing with
                    {
                        Name = !string.IsNullOrWhiteSpace(hero.Title) ? hero.Title : existing.Name,
                        Title = !string.IsNullOrWhiteSpace(hero.Name) ? hero.Name : existing.Title,
                        Alias = !string.IsNullOrWhiteSpace(hero.Alias) ? hero.Alias : existing.Alias,
                        Roles = hero.Roles is { Count: > 0 } ? hero.Roles : existing.Roles
                    };
                else

                    updated[id] = new ChampionStaticInfo(
                        id,
                        !string.IsNullOrWhiteSpace(hero.Title) ? hero.Title : hero.Name,
                        hero.Alias,
                        hero.Name,
                        hero.Roles ?? [],
                        $"/lol-game-data/assets/v1/champion-icons/{id}.png");
            }

            _champions = updated;
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _isInitializing, 1, 0) != 0)
            return;

        try
        {
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                if (_gtimgClient is not null)
                    try
                    {
                        var list = await _gtimgClient.GetHeroListAsync(cancellationToken).ConfigureAwait(false);
                        if (list is not null)
                            UpdateFromGtimg(list);
                    }
                    catch (Exception)
                    {
                    }

                if (_gameDataApi is not null)
                    try
                    {
                        var lcuChamps = await _gameDataApi.GetChampionSummaryAsync(cancellationToken)
                            .ConfigureAwait(false);
                        if (lcuChamps.Count > 0)
                            UpdateFromLcu(lcuChamps);
                    }
                    catch (Exception)
                    {
                    }

                lock (_lock)
                {
                    if (_champions.Count > 0)
                        return;
                }

                if (attempt < 3)
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)), cancellationToken)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
            }
        }
        finally
        {
            Interlocked.Exchange(ref _isInitializing, 0);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            (_gtimgClient as IDisposable)?.Dispose();
        }
    }

    private static int CalculateMatchScore(ChampionStaticInfo champ, string query, bool isNumeric, int numericId)
    {
        if (isNumeric && champ.Id == numericId) return 1000;

        // Exact match
        if (string.Equals(champ.Name, query, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(champ.Title, query, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(champ.Alias, query, StringComparison.OrdinalIgnoreCase))
            return 900;

        // Prefix match
        if (champ.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase) ||
            champ.Title.StartsWith(query, StringComparison.OrdinalIgnoreCase) ||
            champ.Alias.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            return 600;

        // Substring match
        if (champ.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            champ.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            champ.Alias.Contains(query, StringComparison.OrdinalIgnoreCase))
            return 200;

        return 0;
    }
}