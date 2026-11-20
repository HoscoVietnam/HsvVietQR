using System.Text.Json;

namespace VietQR.Core;

/// <summary>
/// Danh mục ngân hàng được nhúng trực tiếp trong assembly VietQR.Core (chạy 100% offline, zero config).
/// </summary>
public sealed class EmbeddedBankCatalog : IBankCatalog
{
    private static readonly Lazy<EmbeddedBankCatalog> _lazy = new(() => new EmbeddedBankCatalog());
    public static EmbeddedBankCatalog Instance => _lazy.Value;

    private readonly Dictionary<string, BankInfo> _byBin = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, BankInfo> _byCode = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<BankInfo> _allBanks = new();

    public EmbeddedBankCatalog()
    {
        LoadEmbeddedCatalog();
    }

    private void LoadEmbeddedCatalog()
    {
        try
        {
            var assembly = typeof(EmbeddedBankCatalog).Assembly;
            using var stream = assembly.GetManifestResourceStream("VietQR.Core.Resources.banks.json");
            if (stream == null) return;

            using var doc = JsonDocument.Parse(stream);
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array) return;

            foreach (var item in data.EnumerateArray())
            {
                var bin = item.TryGetProperty("bin", out var b) ? b.GetString() : null;
                var code = item.TryGetProperty("code", out var c) ? c.GetString() : null;
                var shortName = item.TryGetProperty("shortName", out var sn) ? sn.GetString() : null;
                var name = item.TryGetProperty("name", out var n) ? n.GetString() : null;
                var logo = item.TryGetProperty("logo", out var l) ? l.GetString() : null;

                if (!string.IsNullOrEmpty(bin))
                {
                    var info = new BankInfo(bin, code ?? "", shortName ?? code ?? bin, name ?? "", logo ?? "");
                    _allBanks.Add(info);
                    _byBin[bin] = info;
                    if (!string.IsNullOrEmpty(code)) _byCode[code] = info;
                    if (!string.IsNullOrEmpty(shortName)) _byCode[shortName] = info;
                }
            }

            RegisterCommonAliases();
        }
        catch
        {
            // Bỏ qua lỗi nếu resource chưa sẵn sàng
        }
    }

    private void RegisterCommonAliases()
    {
        string[,] aliases =
        {
            { "970422", "MBBANK" }, { "970422", "QUANDOI" },
            { "970436", "VIETCOMBANK" }, { "970436", "VCBDIGIBANK" },
            { "970415", "VIETINBANK" }, { "970415", "CTG" }, { "970415", "IPAY" },
            { "970418", "BIDVSMARTBANKING" },
            { "970405", "AGRIBANK" }, { "970405", "VARB" },
            { "970407", "TECHCOMBANK" },
            { "970416", "ACBONE" },
            { "970432", "VPBANK" }, { "970432", "VPBANKNEO" },
            { "970423", "TPBANK" },
            { "970403", "SACOMBANK" },
            { "970437", "HDBANK" },
            { "970449", "LPBANK" }, { "970449", "LIENVIETPOSTBANK" }, { "970449", "LVB" },
            { "970448", "OCBOMNI" },
            { "970440", "SEABANK" },
            { "970431", "EXIMBANK" },
            { "970425", "ABBANK" },
            { "970412", "PVCOMBANK" },
            { "970428", "NAMABANK" },
            { "970454", "BVBANK" }, { "970454", "VIETCAPITALBANK" }, { "970454", "DIGIMI" },
            { "970409", "BACABANK" },
            { "970452", "KIENLONGBANK" },
            { "970438", "BAOVIETBANK" },
            { "970400", "SAIGONBANK" },
            { "970424", "SHINHANBANK" }, { "970424", "SHINHAN" },
            { "970457", "WOORIBANK" }, { "970457", "WOORI" },
            { "970439", "PUBLICBANK" },
            { "971005", "VIETTELMONEY" }, { "971005", "VIETTELPAY" },
            { "971011", "VNPTPAY" }, { "971011", "VNPTMONEY" },
            { "971025", "VIMOMO" },
            { "971101", "VIZALOPAY" }, { "971101", "ZLP" }
        };

        int count = aliases.GetLength(0);
        for (int i = 0; i < count; i++)
        {
            var bin = aliases[i, 0];
            var alias = aliases[i, 1];
            if (_byBin.TryGetValue(bin, out var info) && !_byCode.ContainsKey(alias))
            {
                _byCode[alias] = info;
            }
        }
    }

    public Task<BankInfo?> FindAsync(string binOrCode, CancellationToken ct = default)
    {
        return Task.FromResult(Find(binOrCode));
    }

    public BankInfo? Find(string? binOrCode)
    {
        if (string.IsNullOrWhiteSpace(binOrCode)) return null;
        var key = binOrCode.Trim();

        if (_byBin.TryGetValue(key, out var byBin)) return byBin;
        if (_byCode.TryGetValue(key, out var byCode)) return byCode;

        return null;
    }

    public IReadOnlyList<BankInfo> GetAll() => _allBanks.AsReadOnly();

    public Task<IReadOnlyList<BankInfo>> GetAllAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<BankInfo>>(_allBanks.AsReadOnly());
    }
}
