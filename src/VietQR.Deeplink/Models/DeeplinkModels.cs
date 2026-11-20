using VietQR.Core;
using VietQR.Deeplink.Client;

namespace VietQR.Deeplink;

/// <summary>
/// Định nghĩa chi tiết cấu hình điều hướng (Deep Link / Universal Link / Scheme / Package / Store ID)
/// của một ứng dụng ngân hàng hoặc ví điện tử.
/// </summary>
public sealed class DeeplinkAppDefinition
{
    /// <summary>
    /// Mã định danh chuẩn của ứng dụng (VD: <c>MB</c>, <c>BIDV</c>, <c>VCB</c>, <c>MOMO</c>).
    /// </summary>
    public required string AppCode { get; init; }

    /// <summary>
    /// Mã BIN 6 chữ số chuẩn NAPAS (hoặc <c>null</c> đối với một số ví điện tử như VNPAY, ShopeePay).
    /// </summary>
    public string? Bin { get; init; }

    /// <summary>
    /// Tên viết tắt của ngân hàng hoặc ví điện tử (VD: <c>MBBank</c>, <c>Vietcombank</c>).
    /// </summary>
    public required string ShortName { get; init; }

    /// <summary>
    /// Tên hiển thị chính thức của ứng dụng trên App Store / Google Play (VD: <c>MB Bank</c>, <c>VCB Digibank</c>).
    /// </summary>
    public required string AppName { get; init; }

    /// <summary>
    /// Danh sách các bí danh (Aliases) hỗ trợ tìm kiếm không phân biệt hoa thường.
    /// </summary>
    public string[] Aliases { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Cho biết ứng dụng có hỗ trợ tự động điền Số tài khoản, Số tiền, Nội dung chuyển khoản hay không.
    /// </summary>
    public bool SupportsAutofill { get; init; }

    /// <summary>
    /// Hỗ trợ điều hướng trên iOS (iPhone/iPad).
    /// </summary>
    public bool SupportsIos { get; init; } = true;

    /// <summary>
    /// Hỗ trợ điều hướng trên Android.
    /// </summary>
    public bool SupportsAndroid { get; init; } = true;

    /// <summary>
    /// Mã <c>app</c> tương ứng trên cổng Universal Autofill (VD: <c>mb</c>, <c>bidv</c>, <c>ocb</c>, <c>tpb</c>, <c>cake</c>, <c>momo</c>, <c>zalopay</c>).
    /// </summary>
    public required string AutofillAppId { get; init; }

    /// <summary>
    /// Custom URL Scheme để mở ứng dụng trên iOS/Android (VD: <c>mbbank://</c>, <c>vcbdigibank://</c>).
    /// </summary>
    public required string CustomScheme { get; init; }

    /// <summary>
    /// Tên gói ứng dụng (Package Name) trên Google Play Store (VD: <c>com.mbmobile</c>).
    /// </summary>
    public required string AndroidPackage { get; init; }

    /// <summary>
    /// Mã định danh ứng dụng trên Apple App Store (VD: <c>1205807363</c>).
    /// </summary>
    public required string IosAppStoreId { get; init; }

    /// <summary>
    /// Tên file icon trong thư mục <c>/icons/</c> (VD: <c>mb.png</c>).
    /// </summary>
    public string IconFileName => $"{AppCode.ToLowerInvariant()}.png";

    /// <summary>
    /// Chuyển đổi sang DTO công khai <see cref="DeeplinkBankAppInfo"/> trả về cho Client SDK / API.
    /// </summary>
    /// <param name="baseUrl">Base URL của máy chủ để tạo đường dẫn tuyệt đối tới icon.</param>
    public DeeplinkBankAppInfo ToClientInfo(string? baseUrl)
    {
        var prefix = string.IsNullOrWhiteSpace(baseUrl) ? string.Empty : baseUrl.TrimEnd('/');
        return new DeeplinkBankAppInfo
        {
            AppCode = AppCode,
            Bin = Bin,
            ShortName = ShortName,
            AppName = AppName,
            Logo = $"{prefix}/icons/{IconFileName}",
            SupportsAutofill = SupportsAutofill,
            SupportsIos = SupportsIos,
            SupportsAndroid = SupportsAndroid
        };
    }
}

/// <summary>
/// Bản ghi phiên điều hướng thanh toán ngắn hạn (Intent) đã được xác thực CRC16 và bóc tách dữ liệu EMVCo.
/// </summary>
public sealed class DeeplinkIntentRecord
{
    /// <summary>
    /// Mã định danh ngắn hạn trên URL (<c>/p/{Token}</c>).
    /// </summary>
    public required string Token { get; init; }

    /// <summary>
    /// Thông tin ứng dụng ngân hàng/ví điện tử mục tiêu cần mở.
    /// </summary>
    public required DeeplinkAppDefinition TargetApp { get; init; }

    /// <summary>
    /// Chuỗi EMVCo VietQR nguyên bản (đã kiểm tra CRC16 hợp lệ).
    /// </summary>
    public required string QrContent { get; init; }

    /// <summary>
    /// Mã BIN 6 chữ số của ngân hàng thụ hưởng (trích xuất từ chuỗi QR).
    /// </summary>
    public required string ReceiverBankBin { get; init; }

    /// <summary>
    /// Mã ngân hàng thụ hưởng viết tắt (VD: <c>mb</c>, <c>vcb</c>, <c>icb</c>) dùng cho cấu trúc <c>STK@bank</c>.
    /// </summary>
    public required string ReceiverBankCode { get; init; }

    /// <summary>
    /// Tên hiển thị của ngân hàng thụ hưởng (VD: <c>MBBank</c>, <c>Vietcombank</c>).
    /// </summary>
    public required string ReceiverBankShortName { get; init; }

    /// <summary>
    /// Số tài khoản hoặc số thẻ người nhận.
    /// </summary>
    public required string AccountNo { get; init; }

    /// <summary>
    /// Tên chủ tài khoản thụ hưởng (trích xuất từ Tag 59 nếu có).
    /// </summary>
    public string? AccountName { get; init; }

    /// <summary>
    /// Số tiền giao dịch (VNĐ). Bằng <c>null</c> nếu là mã QR tĩnh.
    /// </summary>
    public decimal? Amount { get; init; }

    /// <summary>
    /// Nội dung chuyển khoản (trích xuất từ Tag 62.08).
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Loại hình dịch vụ chuyển khoản (<c>Account</c> - QRIBFTTA hoặc <c>Card</c> - QRIBFTTC).
    /// </summary>
    public VietQrServiceType ServiceType { get; init; }

    /// <summary>
    /// Thời điểm khởi tạo (UTC).
    /// </summary>
    public DateTime CreatedAtUtc { get; init; }

    /// <summary>
    /// Thời điểm hết hạn (UTC).
    /// </summary>
    public DateTime ExpiresAtUtc { get; init; }

    /// <summary>
    /// Số giây hiệu lực còn lại tính từ thời điểm hiện tại.
    /// </summary>
    public int RemainingSeconds =>
        Math.Max(0, (int)Math.Ceiling((ExpiresAtUtc - DateTime.UtcNow).TotalSeconds));
}

