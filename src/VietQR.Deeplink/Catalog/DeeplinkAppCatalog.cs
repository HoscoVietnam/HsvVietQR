using System.Collections.Frozen;
using VietQR.Deeplink.Client;

namespace VietQR.Deeplink;

/// <summary>
/// Danh mục nhúng sẵn 44+ ứng dụng ngân hàng thương mại, ngân hàng số và ví điện tử tại Việt Nam
/// hỗ trợ điều hướng VietQR Deeplink (Autofill &amp; App Launch).
/// </summary>
public sealed class DeeplinkAppCatalog
{
    /// <summary>
    /// Thực thể Singleton mặc định của danh mục ứng dụng Deeplink.
    /// </summary>
    public static readonly DeeplinkAppCatalog Instance = new();

    private readonly IReadOnlyList<DeeplinkAppDefinition> apps;
    private readonly FrozenDictionary<string, DeeplinkAppDefinition> lookup;

    public DeeplinkAppCatalog()
    {
        apps = CreateDefaultDefinitions();
        var map = new Dictionary<string, DeeplinkAppDefinition>(StringComparer.OrdinalIgnoreCase);

        foreach (var app in apps)
        {
            map[app.AppCode] = app;
            map[app.ShortName] = app;
            map[app.AutofillAppId] = app;

            if (!string.IsNullOrWhiteSpace(app.Bin) && !map.ContainsKey(app.Bin))
            {
                map[app.Bin] = app;
            }

            foreach (var alias in app.Aliases)
            {
                if (!string.IsNullOrWhiteSpace(alias))
                {
                    map[alias] = app;
                }
            }
        }

        lookup = map.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Lấy toàn bộ danh sách các ứng dụng được hỗ trợ.
    /// </summary>
    public IReadOnlyList<DeeplinkAppDefinition> GetAllDefinitions() => apps;

    /// <summary>
    /// Lấy danh sách DTO <see cref="DeeplinkBankAppInfo"/> chuẩn hóa theo Base URL hiện tại.
    /// </summary>
    public IReadOnlyList<DeeplinkBankAppInfo> GetSupportedApps(string? baseUrl = null)
    {
        var result = new DeeplinkBankAppInfo[apps.Count];
        for (int i = 0; i < apps.Count; i++)
        {
            result[i] = apps[i].ToClientInfo(baseUrl);
        }
        return result;
    }

    /// <summary>
    /// Tra cứu cấu hình ứng dụng theo <c>appCode</c>, <c>shortName</c>, <c>bin</c> 6 chữ số hoặc <c>aliases</c> (không phân biệt hoa thường).
    /// </summary>
    public DeeplinkAppDefinition? Find(string? codeOrBinOrAlias)
    {
        if (string.IsNullOrWhiteSpace(codeOrBinOrAlias))
        {
            return null;
        }

        return lookup.TryGetValue(codeOrBinOrAlias.Trim(), out var found) ? found : null;
    }

    private static DeeplinkAppDefinition[] CreateDefaultDefinitions() =>
    [
        // =========================================================================================
        // NHÓM 1: 7 ỨNG DỤNG HỖ TRỢ AUTOFILL (MỞ APP + TỰ ĐIỀN STK, NGÂN HÀNG, SỐ TIỀN, NỘI DUNG)
        // =========================================================================================
        new()
        {
            AppCode = "MB",
            Bin = "970422",
            ShortName = "MBBank",
            AppName = "MB Bank",
            Aliases = ["MBBANK", "MB", "QUANDOI"],
            SupportsAutofill = true,
            AutofillAppId = "mb",
            CustomScheme = "mbbank://",
            AndroidPackage = "com.mbmobile",
            IosAppStoreId = "1205807363"
        },
        new()
        {
            AppCode = "BIDV",
            Bin = "970418",
            ShortName = "BIDV",
            AppName = "BIDV SmartBanking",
            Aliases = ["BIDVSMARTBANKING"],
            SupportsAutofill = true,
            AutofillAppId = "bidv",
            CustomScheme = "bidvsmartbanking://",
            AndroidPackage = "com.vnpay.bidv",
            IosAppStoreId = "1061867449"
        },
        new()
        {
            AppCode = "OCB",
            Bin = "970448",
            ShortName = "OCB",
            AppName = "OCB OMNI",
            Aliases = ["OCBOMNI", "PHUONGDONG"],
            SupportsAutofill = true,
            AutofillAppId = "ocb",
            CustomScheme = "ocbomni://",
            AndroidPackage = "vn.com.ocb.awe",
            IosAppStoreId = "1358682577"
        },
        new()
        {
            AppCode = "TPB",
            Bin = "970423",
            ShortName = "TPBank",
            AppName = "TPBank Mobile",
            Aliases = ["TPBANK", "TIENPHONG"],
            SupportsAutofill = true,
            AutofillAppId = "tpb",
            CustomScheme = "tpbank://",
            AndroidPackage = "com.tpb.mb.gprsandroid",
            IosAppStoreId = "450464147"
        },
        new()
        {
            AppCode = "CAKE",
            Bin = "546034",
            ShortName = "CAKE",
            AppName = "CAKE - Ngân hàng số",
            Aliases = ["CAKEBYVPBANK"],
            SupportsAutofill = true,
            AutofillAppId = "cake",
            CustomScheme = "cake.vn://",
            AndroidPackage = "xyz.be.cake",
            IosAppStoreId = "1551907051"
        },
        new()
        {
            AppCode = "MOMO",
            Bin = "971025",
            ShortName = "MoMo",
            AppName = "MoMo - Trợ Thủ Tài Chính",
            Aliases = ["VIMOMO"],
            SupportsAutofill = true,
            AutofillAppId = "momo",
            CustomScheme = "momo://",
            AndroidPackage = "com.mservice.momotransfer",
            IosAppStoreId = "918751511"
        },
        new()
        {
            AppCode = "ZALOPAY",
            Bin = "971101",
            ShortName = "ZaloPay",
            AppName = "Zalopay - Thanh toán & Tài chính",
            Aliases = ["VIZALOPAY", "ZLP"],
            SupportsAutofill = true,
            AutofillAppId = "zalopay",
            CustomScheme = "zalopay://",
            AndroidPackage = "vn.com.vng.zalopay",
            IosAppStoreId = "1112407590"
        },

        // =========================================================================================
        // NHÓM 2: 37+ ỨNG DỤNG NGÂN HÀNG & VÍ ĐIỆN TỬ MỞ TRỰC TIẾP ỨNG DỤNG (APP LAUNCH)
        // =========================================================================================
        new()
        {
            AppCode = "VCB",
            Bin = "970436",
            ShortName = "Vietcombank",
            AppName = "VCB Digibank",
            Aliases = ["VIETCOMBANK", "VCBDIGIBANK"],
            SupportsAutofill = false,
            AutofillAppId = "vcb",
            CustomScheme = "vcbdigibank://",
            AndroidPackage = "com.VCB",
            IosAppStoreId = "561433133"
        },
        new()
        {
            AppCode = "ICB",
            Bin = "970415",
            ShortName = "VietinBank",
            AppName = "VietinBank iPay",
            Aliases = ["VIETINBANK", "CTG", "IPAY"],
            SupportsAutofill = false,
            AutofillAppId = "icb",
            CustomScheme = "vietinbankmobile://",
            AndroidPackage = "com.vietinbank.ipay",
            IosAppStoreId = "689963454"
        },
        new()
        {
            AppCode = "VBA",
            Bin = "970405",
            ShortName = "Agribank",
            AppName = "Agribank Plus",
            Aliases = ["AGRIBANK", "AGRIBANKPLUS", "VARB"],
            SupportsAutofill = false,
            AutofillAppId = "vba",
            CustomScheme = "agribankmobile://",
            AndroidPackage = "com.vnpay.Agribank3g",
            IosAppStoreId = "935944952"
        },
        new()
        {
            AppCode = "TCB",
            Bin = "970407",
            ShortName = "Techcombank",
            AppName = "Techcombank Mobile",
            Aliases = ["TECHCOMBANK"],
            SupportsAutofill = false,
            AutofillAppId = "tcb",
            CustomScheme = "techcombank://",
            AndroidPackage = "vn.com.techcombank.bb.app",
            IosAppStoreId = "1548623362"
        },
        new()
        {
            AppCode = "ACB",
            Bin = "970416",
            ShortName = "ACB",
            AppName = "ACB ONE",
            Aliases = ["ACBONE", "ACHAU"],
            SupportsAutofill = false,
            AutofillAppId = "acb",
            CustomScheme = "acbone://",
            AndroidPackage = "mobile.acb.com.vn",
            IosAppStoreId = "950141024"
        },
        new()
        {
            AppCode = "VPB",
            Bin = "970432",
            ShortName = "VPBank",
            AppName = "VPBank NEO",
            Aliases = ["VPBANK", "VPBANKNEO"],
            SupportsAutofill = false,
            AutofillAppId = "vpb",
            CustomScheme = "vpbankneo://",
            AndroidPackage = "com.vnpay.vpbankonline",
            IosAppStoreId = "1209349510"
        },
        new()
        {
            AppCode = "VIB",
            Bin = "970441",
            ShortName = "VIB",
            AppName = "MyVIB",
            Aliases = ["MYVIB"],
            SupportsAutofill = false,
            AutofillAppId = "vib",
            CustomScheme = "myvib2://",
            AndroidPackage = "com.vib.myvib2",
            IosAppStoreId = "1626624790"
        },
        new()
        {
            AppCode = "STB",
            Bin = "970403",
            ShortName = "Sacombank",
            AppName = "SACOMBANK PAY",
            Aliases = ["SACOMBANK", "SACOMBANKPAY"],
            SupportsAutofill = false,
            AutofillAppId = "stb",
            CustomScheme = "sacombankpay://",
            AndroidPackage = "com.sacombank.ewallet",
            IosAppStoreId = "1436283273"
        },
        new()
        {
            AppCode = "HDB",
            Bin = "970437",
            ShortName = "HDBank",
            AppName = "HDBank",
            Aliases = ["HDBANK"],
            SupportsAutofill = false,
            AutofillAppId = "hdb",
            CustomScheme = "hdbank://",
            AndroidPackage = "com.vnpay.hdbank",
            IosAppStoreId = "1461658565"
        },
        new()
        {
            AppCode = "SHB",
            Bin = "970443",
            ShortName = "SHB",
            AppName = "SHB SAHA",
            Aliases = ["SHBSAHA"],
            SupportsAutofill = false,
            AutofillAppId = "shb",
            CustomScheme = "shbsaha://",
            AndroidPackage = "vn.shb.mbanking",
            IosAppStoreId = "538278798"
        },
        new()
        {
            AppCode = "LPB",
            Bin = "970449",
            ShortName = "LPBank",
            AppName = "LPBank Plus",
            Aliases = ["LPBANK", "LIENVIETPOSTBANK", "LVB"],
            SupportsAutofill = false,
            AutofillAppId = "lpb",
            CustomScheme = "lv24h://",
            AndroidPackage = "vn.com.lpb.lienviet24h",
            IosAppStoreId = "1488794748"
        },
        new()
        {
            AppCode = "MSB",
            Bin = "970426",
            ShortName = "MSB",
            AppName = "MSB mBank",
            Aliases = ["MSBBANK", "MARITIMEBANK"],
            SupportsAutofill = false,
            AutofillAppId = "msb",
            CustomScheme = "msbmbank://",
            AndroidPackage = "vn.com.msb.smartbanking",
            IosAppStoreId = "436134873"
        },
        new()
        {
            AppCode = "SEAB",
            Bin = "970440",
            ShortName = "SeABank",
            AppName = "SeAMobile",
            Aliases = ["SEABANK", "SEAMOBILE"],
            SupportsAutofill = false,
            AutofillAppId = "seab",
            CustomScheme = "seamobile://",
            AndroidPackage = "vn.com.seabank.mb1",
            IosAppStoreId = "1464071528"
        },
        new()
        {
            AppCode = "EIB",
            Bin = "970431",
            ShortName = "Eximbank",
            AppName = "Eximbank EDigi",
            Aliases = ["EXIMBANK", "EDIGI"],
            SupportsAutofill = false,
            AutofillAppId = "eib",
            CustomScheme = "eximbankedigi://",
            AndroidPackage = "com.vnpay.eximbank",
            IosAppStoreId = "1572427319"
        },
        new()
        {
            AppCode = "ABB",
            Bin = "970425",
            ShortName = "ABBANK",
            AppName = "ABBank",
            Aliases = ["ABBANK", "ANBINH"],
            SupportsAutofill = false,
            AutofillAppId = "abb",
            CustomScheme = "abbank://",
            AndroidPackage = "com.vnpay.abbank",
            IosAppStoreId = "1137160023"
        },
        new()
        {
            AppCode = "PVCB",
            Bin = "970412",
            ShortName = "PVcomBank",
            AppName = "PVConnect",
            Aliases = ["PVCOMBANK", "PVCONNECT"],
            SupportsAutofill = false,
            AutofillAppId = "pvcb",
            CustomScheme = "pvcombank://",
            AndroidPackage = "com.vsii.pvcombank",
            IosAppStoreId = "957284067"
        },
        new()
        {
            AppCode = "NAB",
            Bin = "970428",
            ShortName = "NamABank",
            AppName = "Nam A Bank Open Banking",
            Aliases = ["NAMABANK"],
            SupportsAutofill = false,
            AutofillAppId = "nab",
            CustomScheme = "namabank://",
            AndroidPackage = "ops.namabank.com.vn",
            IosAppStoreId = "1227187853"
        },
        new()
        {
            AppCode = "VCCB",
            Bin = "970454",
            ShortName = "BVBank",
            AppName = "digimi - Ngân hàng số",
            Aliases = ["BVBANK", "VIETCAPITALBANK", "DIGIMI"],
            SupportsAutofill = false,
            AutofillAppId = "vccb",
            CustomScheme = "digimi://",
            AndroidPackage = "vn.banvietbank.mobilebanking",
            IosAppStoreId = "1526444697"
        },
        new()
        {
            AppCode = "VIETBANK",
            Bin = "970433",
            ShortName = "VietBank",
            AppName = "Vietbank Digital Plus",
            Aliases = ["VB"],
            SupportsAutofill = false,
            AutofillAppId = "vietbank",
            CustomScheme = "vietbankdigital://",
            AndroidPackage = "com.vnpay.vietbank",
            IosAppStoreId = "1469883896"
        },
        new()
        {
            AppCode = "BAB",
            Bin = "970409",
            ShortName = "BacABank",
            AppName = "BAC A BANK Mobile Banking",
            Aliases = ["BACABANK"],
            SupportsAutofill = false,
            AutofillAppId = "bab",
            CustomScheme = "bacabank://",
            AndroidPackage = "com.bab.retailUAT",
            IosAppStoreId = "1441408786"
        },
        new()
        {
            AppCode = "NCB",
            Bin = "970419",
            ShortName = "NCB",
            AppName = "NCB iziMobile",
            Aliases = ["NCBIZIMOBILE"],
            SupportsAutofill = false,
            AutofillAppId = "ncb",
            CustomScheme = "ncbizimobile://",
            AndroidPackage = "com.ncb.bank",
            IosAppStoreId = "1465217154"
        },
        new()
        {
            AppCode = "SCB",
            Bin = "970429",
            ShortName = "SCB",
            AppName = "SCB Mobile Banking",
            Aliases = ["SAIGONBANKSCB"],
            SupportsAutofill = false,
            AutofillAppId = "scb",
            CustomScheme = "scbmobilebanking://",
            AndroidPackage = "com.vnpay.scb",
            IosAppStoreId = "954973621"
        },
        new()
        {
            AppCode = "KLB",
            Bin = "970452",
            ShortName = "KienLongBank",
            AppName = "KienlongBank Plus",
            Aliases = ["KIENLONGBANK"],
            SupportsAutofill = false,
            AutofillAppId = "klb",
            CustomScheme = "kienlongbankplus://",
            AndroidPackage = "com.sunshine.ksbank",
            IosAppStoreId = "1562823941"
        },
        new()
        {
            AppCode = "BVB",
            Bin = "970438",
            ShortName = "BaoVietBank",
            AppName = "BAOVIET Smart",
            Aliases = ["BAOVIETBANK", "BAOVIETSMART"],
            SupportsAutofill = false,
            AutofillAppId = "bvb",
            CustomScheme = "baovietsmart://",
            AndroidPackage = "com.vnpay.bvbank",
            IosAppStoreId = "1504422967"
        },
        new()
        {
            AppCode = "SGICB",
            Bin = "970400",
            ShortName = "SaigonBank",
            AppName = "SAIGONBANK Smart Banking",
            Aliases = ["SAIGONBANK", "SGB"],
            SupportsAutofill = false,
            AutofillAppId = "sgicb",
            CustomScheme = "saigonbanksmartbanking://",
            AndroidPackage = "com.vnpay.sgbank",
            IosAppStoreId = "1481832587"
        },
        new()
        {
            AppCode = "SHBVN",
            Bin = "970424",
            ShortName = "ShinhanBank",
            AppName = "Shinhan SOL Việt Nam",
            Aliases = ["SHINHANBANK", "SHINHAN", "SHINHANSOL"],
            SupportsAutofill = false,
            AutofillAppId = "shbvn",
            CustomScheme = "shinhansolvn://",
            AndroidPackage = "com.shinhan.global.vn.bank",
            IosAppStoreId = "1071033810"
        },
        new()
        {
            AppCode = "WVN",
            Bin = "970457",
            ShortName = "Woori",
            AppName = "Woori WON Vietnam",
            Aliases = ["WOORI", "WOORIBANK"],
            SupportsAutofill = false,
            AutofillAppId = "wvn",
            CustomScheme = "wvbs://",
            AndroidPackage = "vn.com.woori.smart",
            IosAppStoreId = "1501785125"
        },
        new()
        {
            AppCode = "PBVN",
            Bin = "970439",
            ShortName = "PublicBank",
            AppName = "PB engage VN",
            Aliases = ["PUBLICBANK"],
            SupportsAutofill = false,
            AutofillAppId = "pbvn",
            CustomScheme = "pbengagevn://",
            AndroidPackage = "com.PBVN.PBengage",
            IosAppStoreId = "1573736472"
        },
        new()
        {
            AppCode = "CIMB",
            Bin = "422589",
            ShortName = "CIMB",
            AppName = "Octo by CIMB Vietnam",
            Aliases = ["CIMBBANK", "OCTO"],
            SupportsAutofill = false,
            AutofillAppId = "cimb",
            CustomScheme = "cimb://",
            AndroidPackage = "vn.cimbbank.octo",
            IosAppStoreId = "1318127958"
        },
        new()
        {
            AppCode = "COOPBANK",
            Bin = "970446",
            ShortName = "COOPBANK",
            AppName = "Co-opBank Mobile Banking",
            Aliases = ["COOP"],
            SupportsAutofill = false,
            AutofillAppId = "coopbank",
            CustomScheme = "coopbankmobile://",
            AndroidPackage = "com.vnpay.coopbank",
            IosAppStoreId = "1578445811"
        },

        // --- Ngân hàng số còn lại ---
        new()
        {
            AppCode = "TIMO",
            Bin = "963388",
            ShortName = "Timo",
            AppName = "Timo - Ngân hàng số by BVBank",
            Aliases = ["TIMOBANK"],
            SupportsAutofill = false,
            AutofillAppId = "timo",
            CustomScheme = "timo://",
            AndroidPackage = "io.lifestyle.timo",
            IosAppStoreId = "1521230347"
        },
        new()
        {
            AppCode = "TNEX",
            Bin = "963326",
            ShortName = "TNEX",
            AppName = "TNEX - Tài chính số toàn diện",
            Aliases = ["TNEXBANK"],
            SupportsAutofill = false,
            AutofillAppId = "tnex",
            CustomScheme = "tnex://",
            AndroidPackage = "vn.tnex.consumer",
            IosAppStoreId = "1527220699"
        },
        new()
        {
            AppCode = "LIOBANK",
            Bin = "970448",
            ShortName = "Liobank",
            AppName = "Liobank: Ngân hàng số",
            Aliases = ["LIO"],
            SupportsAutofill = false,
            AutofillAppId = "liobank",
            CustomScheme = "liobank://",
            AndroidPackage = "com.ocb.liobank",
            IosAppStoreId = "6444038728"
        },

        // --- Ví điện tử & Trung gian thanh toán còn lại ---
        new()
        {
            AppCode = "VNPAY",
            Bin = null,
            ShortName = "VNPAY",
            AppName = "Ứng dụng VNPAY",
            Aliases = ["VIVNPAY", "VNPAYWALLET"],
            SupportsAutofill = false,
            AutofillAppId = "vnpay",
            CustomScheme = "vnpaywallet://",
            AndroidPackage = " com.vnpay.ewallet".Trim(),
            IosAppStoreId = "1470378562"
        },
        new()
        {
            AppCode = "SHOPEEPAY",
            Bin = null,
            ShortName = "ShopeePay",
            AppName = "ShopeePay - Tài Chính Dễ Dàng",
            Aliases = ["AIRPAY", "VISHOPEEPAY"],
            SupportsAutofill = false,
            AutofillAppId = "shopeepay",
            CustomScheme = "shopeepay://",
            AndroidPackage = "com.beeasy.toppay",
            IosAppStoreId = "1032301823"
        },
        new()
        {
            AppCode = "VTLMONEY",
            Bin = "971005",
            ShortName = "ViettelMoney",
            AppName = "Viettel Money",
            Aliases = ["VIETTELMONEY", "VIETTELPAY"],
            SupportsAutofill = false,
            AutofillAppId = "vtlmoney",
            CustomScheme = "viettelpay://",
            AndroidPackage = "com.bplus.vtpay",
            IosAppStoreId = "1344204781"
        },
        new()
        {
            AppCode = "VNPTMONEY",
            Bin = "971011",
            ShortName = "VNPTMoney",
            AppName = "VNPT Money",
            Aliases = ["VNPTPAY"],
            SupportsAutofill = false,
            AutofillAppId = "vnptmoney",
            CustomScheme = "vnptpay://",
            AndroidPackage = "com.vnptpay",
            IosAppStoreId = "1294946479"
        }
    ];
}

