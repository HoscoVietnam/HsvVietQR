namespace VietQR.Deeplink;

/// <summary>
/// Cấu hình cho máy chủ VietQR Deeplink Engine (đọc từ section <c>"Deeplink"</c> trong <c>appsettings.json</c>).
/// </summary>
public sealed class DeeplinkServerOptions
{
    /// <summary>
    /// Tên section mặc định trong cấu hình.
    /// </summary>
    public const string SectionName = "Deeplink";

    /// <summary>
    /// Domain công khai của cổng Deeplink (ví dụ: <c>https://vietqr.hiendev.io.vn</c>).
    /// Nếu để trống hoặc chạy local, hệ thống tự động lấy theo Host/Scheme của HTTP Request hiện tại.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    /// Thời gian hiệu lực của mỗi liên kết điều hướng <c>/p/{token}</c> tính bằng giây (Mặc định: 300 giây = 5 phút).
    /// </summary>
    public int IntentTtlSeconds { get; set; } = 300;

    /// <summary>
    /// Số lượng phiên Intent tối đa lưu đồng thời trong bộ nhớ RAM (Mặc định: 50.000 phiên).
    /// </summary>
    public int MaxActiveIntents { get; set; } = 50_000;

    /// <summary>
    /// Khóa bí mật dùng để ký/mã hóa Stateless Token dự phòng (nếu để trống sẽ tự sinh ngẫu nhiên khi khởi động).
    /// </summary>
    public string? TokenSecret { get; set; }

    /// <summary>
    /// Cổng Universal Link hỗ trợ tự động điền thông tin chuyển khoản cho nhóm ngân hàng Autofill.
    /// Mặc định: <c>https://dl.vietqr.io/pay</c>.
    /// </summary>
    public string AutofillGatewayUrl { get; set; } = "https://dl.vietqr.io/pay";
}

