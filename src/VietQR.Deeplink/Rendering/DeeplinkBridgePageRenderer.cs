using System.Globalization;
using System.Net;
using System.Text;
using System.Web;

namespace VietQR.Deeplink;

/// <summary>
/// Bộ dựng đường dẫn điều hướng sâu (Deep Link / Universal Link / Android Intent) và giao diện
/// trang cầu nối thanh toán thông minh (<c>/p/{token}</c>) thích ứng theo thiết bị (iOS, Android, Desktop).
/// </summary>
public static class DeeplinkBridgePageRenderer
{
    /// <summary>
    /// Tạo đường dẫn Autofill chuẩn (<c>https://dl.vietqr.io/pay?app=...</c>) cho các ứng dụng hỗ trợ tự điền.
    /// </summary>
    public static string BuildAutofillUrl(DeeplinkIntentRecord record, string autofillGatewayUrl = "https://dl.vietqr.io/pay")
    {
        var gateway = string.IsNullOrWhiteSpace(autofillGatewayUrl)
            ? "https://dl.vietqr.io/pay"
            : autofillGatewayUrl.TrimEnd('/');

        var sb = new StringBuilder(gateway);
        sb.Append("?app=").Append(Uri.EscapeDataString(record.TargetApp.AutofillAppId));

        var ba = $"{record.AccountNo}@{record.ReceiverBankCode}";
        sb.Append("&ba=").Append(Uri.EscapeDataString(ba));

        if (record.Amount.HasValue && record.Amount.Value > 0)
        {
            var amountInt = Convert.ToInt64(Math.Round(record.Amount.Value, 0, MidpointRounding.AwayFromZero));
            sb.Append("&am=").Append(amountInt.ToString(CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrWhiteSpace(record.Description))
        {
            sb.Append("&tn=").Append(Uri.EscapeDataString(record.Description));
        }

        if (!string.IsNullOrWhiteSpace(record.AccountName))
        {
            sb.Append("&bn=").Append(Uri.EscapeDataString(record.AccountName));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Tạo URI <c>intent://</c> chuẩn Android Chrome giúp mở thẳng App ngân hàng hoặc fallback an toàn.
    /// </summary>
    public static string BuildAndroidIntentUri(DeeplinkAppDefinition app, string fallbackWebUrl)
    {
        var rawScheme = app.CustomScheme.Replace("://", string.Empty).Trim('/');
        var encodedFallback = Uri.EscapeDataString(fallbackWebUrl);
        return $"intent://pay#Intent;scheme={rawScheme};package={app.AndroidPackage};S.browser_fallback_url={encodedFallback};end";
    }

    /// <summary>
    /// Render trang HTML5 Smart Payment Bridge khi người dùng truy cập <c>GET /p/{token}</c>.
    /// </summary>
    public static string RenderPaymentBridgeHtml(
        DeeplinkIntentRecord record,
        string? userAgent,
        string currentBaseUrl,
        string autofillGatewayUrl)
    {
        var ua = userAgent ?? string.Empty;
        bool isAndroid = ua.Contains("Android", StringComparison.OrdinalIgnoreCase);
        bool isIos = ua.Contains("iPhone", StringComparison.OrdinalIgnoreCase) ||
                     ua.Contains("iPad", StringComparison.OrdinalIgnoreCase) ||
                     ua.Contains("iPod", StringComparison.OrdinalIgnoreCase);
        bool isMobile = isAndroid || isIos;

        var app = record.TargetApp;
        var playStoreUrl = $"https://play.google.com/store/apps/details?id={Uri.EscapeDataString(app.AndroidPackage)}";
        var appStoreUrl = $"https://apps.apple.com/vn/app/id{Uri.EscapeDataString(app.IosAppStoreId)}";
        var storeUrl = isIos ? appStoreUrl : playStoreUrl;

        var autofillUrl = BuildAutofillUrl(record, autofillGatewayUrl);
        var androidIntentUri = BuildAndroidIntentUri(app, playStoreUrl);
        var iosSchemeUri = app.CustomScheme;

        // Xác định URL mở ứng dụng chính theo nhóm Autofill vs App Launch và theo hệ điều hành
        string primaryLaunchUrl;
        if (app.SupportsAutofill)
        {
            primaryLaunchUrl = autofillUrl;
        }
        else if (isAndroid)
        {
            primaryLaunchUrl = androidIntentUri;
        }
        else if (isIos)
        {
            primaryLaunchUrl = iosSchemeUri;
        }
        else
        {
            primaryLaunchUrl = autofillUrl;
        }

        var iconUrl = $"{currentBaseUrl.TrimEnd('/')}/icons/{app.IconFileName}";
        var qrImageUrl = $"{currentBaseUrl.TrimEnd('/')}/p/{Uri.EscapeDataString(record.Token)}/qr.png";
        var formattedAmount = record.Amount.HasValue && record.Amount.Value > 0
            ? string.Format(CultureInfo.GetCultureInfo("vi-VN"), "{0:N0} đ", record.Amount.Value)
            : "Tự nhập số tiền";
        var rawAmount = record.Amount.HasValue && record.Amount.Value > 0
            ? Convert.ToInt64(record.Amount.Value).ToString(CultureInfo.InvariantCulture)
            : string.Empty;

        var safeAppName = WebUtility.HtmlEncode(app.AppName);
        var safeReceiverBank = WebUtility.HtmlEncode($"{record.ReceiverBankShortName} ({record.ReceiverBankBin})");
        var safeAccountNo = WebUtility.HtmlEncode(record.AccountNo);
        var accountFieldLabel = record.ServiceType == VietQR.Core.VietQrServiceType.Card ? "Số thẻ" : "Số tài khoản";
        var safeAccountName = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(record.AccountName) ? "—" : record.AccountName);
        var safeDescription = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(record.Description) ? "—" : record.Description);
        var badgeText = app.SupportsAutofill
            ? "Hỗ trợ tự động điền STK &amp; Số tiền"
            : "Mở ứng dụng ngân hàng để quét QR";
        var badgeColor = app.SupportsAutofill
            ? "background:#ecfdf5;color:#047857;border:1px solid #a7f3d0;"
            : "background:#eff6ff;color:#1d4ed8;border:1px solid #bfdbfe;";

        var autoOpenScript = isMobile
            ? $$"""
                setTimeout(function() {
                    launchBankingApp();
                }, 250);
              """
            : string.Empty;

        return $$"""
<!DOCTYPE html>
<html lang="vi">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1" />
  <meta name="robots" content="noindex, nofollow" />
  <title>Thanh toán VietQR qua {{safeAppName}}</title>
  <style>
    :root { color-scheme: light; }
    * { box-sizing: border-box; margin: 0; padding: 0; }
    body {
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif;
      background: linear-gradient(145deg, #f1f5f9 0%, #e2e8f0 100%);
      color: #0f172a;
      min-height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 16px;
    }
    .card {
      background: #ffffff;
      width: 100%;
      max-width: 430px;
      border-radius: 24px;
      box-shadow: 0 20px 40px -15px rgba(15, 23, 42, 0.12), 0 0 1px 1px rgba(15, 23, 42, 0.05);
      overflow: hidden;
    }
    .header {
      padding: 20px 22px 16px;
      border-bottom: 1px solid #f1f5f9;
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
    }
    .app-meta {
      display: flex;
      align-items: center;
      gap: 12px;
      min-width: 0;
    }
    .app-icon {
      width: 48px;
      height: 48px;
      border-radius: 14px;
      object-fit: contain;
      background: #f8fafc;
      border: 1px solid #e2e8f0;
      padding: 4px;
      flex-shrink: 0;
    }
    .app-title {
      font-size: 16px;
      font-weight: 700;
      color: #0f172a;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
    .badge {
      display: inline-block;
      margin-top: 4px;
      font-size: 11px;
      font-weight: 600;
      padding: 2px 8px;
      border-radius: 999px;
      {{badgeColor}}
    }
    .timer {
      font-size: 12px;
      font-weight: 700;
      color: #475569;
      background: #f8fafc;
      border: 1px solid #e2e8f0;
      padding: 6px 10px;
      border-radius: 10px;
      white-space: nowrap;
      font-variant-numeric: tabular-nums;
    }
    .body { padding: 20px 22px 24px; }
    .qr-box {
      background: #f8fafc;
      border: 1px dashed #cbd5e1;
      border-radius: 18px;
      padding: 14px;
      text-align: center;
      margin-bottom: 16px;
    }
    .qr-img {
      width: 220px;
      height: auto;
      max-width: 100%;
      border-radius: 12px;
      display: block;
      margin: 0 auto;
    }
    .qr-hint {
      font-size: 12px;
      color: #64748b;
      margin-top: 8px;
    }
    .info-list {
      background: #f8fafc;
      border: 1px solid #e2e8f0;
      border-radius: 16px;
      padding: 6px 14px;
      margin-bottom: 18px;
    }
    .info-row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 9px 0;
      border-bottom: 1px solid #e2e8f0;
      gap: 8px;
      font-size: 13px;
    }
    .info-row:last-child { border-bottom: none; }
    .info-label { color: #64748b; flex-shrink: 0; }
    .info-val-wrap {
      display: flex;
      align-items: center;
      gap: 6px;
      min-width: 0;
      text-align: right;
    }
    .info-val {
      font-weight: 600;
      color: #0f172a;
      word-break: break-all;
    }
    .info-val.amount {
      color: #047857;
      font-size: 15px;
      font-weight: 700;
    }
    .btn-copy {
      background: #ffffff;
      border: 1px solid #cbd5e1;
      color: #334155;
      font-size: 11px;
      font-weight: 600;
      padding: 3px 8px;
      border-radius: 6px;
      cursor: pointer;
      flex-shrink: 0;
    }
    .btn-copy:active { background: #e2e8f0; }
    .btn-primary {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 8px;
      width: 100%;
      background: #0f172a;
      color: #ffffff;
      text-decoration: none;
      font-weight: 700;
      font-size: 14px;
      padding: 13px 16px;
      border-radius: 14px;
      border: none;
      cursor: pointer;
      transition: opacity 0.15s;
      margin-bottom: 10px;
    }
    .btn-primary:hover { opacity: 0.92; }
    .actions-row {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 8px;
    }
    .btn-secondary {
      display: flex;
      align-items: center;
      justify-content: center;
      text-align: center;
      background: #f1f5f9;
      color: #334155;
      text-decoration: none;
      font-weight: 600;
      font-size: 12px;
      padding: 10px 12px;
      border-radius: 12px;
      border: 1px solid #e2e8f0;
    }
    .toast {
      position: fixed;
      bottom: 20px;
      left: 50%;
      transform: translateX(-50%);
      background: #0f172a;
      color: #fff;
      font-size: 12px;
      font-weight: 600;
      padding: 8px 16px;
      border-radius: 999px;
      opacity: 0;
      pointer-events: none;
      transition: opacity 0.2s;
    }
    .toast.show { opacity: 1; }
  </style>
</head>
<body>
  <div class="card">
    <div class="header">
      <div class="app-meta">
        <img src="{{iconUrl}}" alt="{{safeAppName}}" class="app-icon" onerror="this.style.display='none'" />
        <div>
          <div class="app-title">{{safeAppName}}</div>
          <span class="badge">{{badgeText}}</span>
        </div>
      </div>
      <div class="timer" id="countdown">{{record.RemainingSeconds}}s</div>
    </div>

    <div class="body">
      <div class="qr-box">
        <img src="{{qrImageUrl}}" alt="VietQR Payment" class="qr-img" />
        <div class="qr-hint">Quét mã QR hoặc bấm nút bên dưới để mở ứng dụng</div>
      </div>

      <div class="info-list">
        <div class="info-row">
          <span class="info-label">Ngân hàng nhận</span>
          <div class="info-val-wrap">
            <span class="info-val">{{safeReceiverBank}}</span>
          </div>
        </div>
        <div class="info-row">
          <span class="info-label">{{accountFieldLabel}}</span>
          <div class="info-val-wrap">
            <span class="info-val">{{safeAccountNo}}</span>
            <button type="button" class="btn-copy" onclick="copyText('{{safeAccountNo}}', '{{accountFieldLabel}}')">Sao chép</button>
          </div>
        </div>
        <div class="info-row">
          <span class="info-label">Chủ tài khoản</span>
          <div class="info-val-wrap">
            <span class="info-val">{{safeAccountName}}</span>
          </div>
        </div>
        <div class="info-row">
          <span class="info-label">Số tiền</span>
          <div class="info-val-wrap">
            <span class="info-val amount">{{formattedAmount}}</span>
            {{(string.IsNullOrEmpty(rawAmount) ? "" : $"<button type=\"button\" class=\"btn-copy\" onclick=\"copyText('{rawAmount}', 'Số tiền')\">Sao chép</button>")}}
          </div>
        </div>
        <div class="info-row">
          <span class="info-label">Nội dung CK</span>
          <div class="info-val-wrap">
            <span class="info-val">{{safeDescription}}</span>
            {{(string.IsNullOrWhiteSpace(record.Description) ? "" : $"<button type=\"button\" class=\"btn-copy\" onclick=\"copyText('{HttpUtility.JavaScriptStringEncode(record.Description)}', 'Nội dung')\">Sao chép</button>")}}
          </div>
        </div>
      </div>

      <button type="button" class="btn-primary" onclick="launchBankingApp()">
        <span>Mở ứng dụng {{safeAppName}}</span>
      </button>

      <div class="actions-row">
        <a href="{{qrImageUrl}}?download=1" download="vietqr-{{record.AccountNo}}.png" class="btn-secondary">
          Tải ảnh mã QR
        </a>
        <a href="{{storeUrl}}" target="_blank" rel="noopener" class="btn-secondary">
          Tải App trên Store
        </a>
      </div>
    </div>
  </div>

  <div id="toast" class="toast">Đã sao chép</div>

  <script>
    var primaryUrl = "{{HttpUtility.JavaScriptStringEncode(primaryLaunchUrl)}}";
    var customScheme = "{{HttpUtility.JavaScriptStringEncode(iosSchemeUri)}}";
    var androidIntent = "{{HttpUtility.JavaScriptStringEncode(androidIntentUri)}}";
    var isAndroid = {{(isAndroid ? "true" : "false")}};
    var isIos = {{(isIos ? "true" : "false")}};
    var supportsAutofill = {{(app.SupportsAutofill ? "true" : "false")}};
    var remaining = {{record.RemainingSeconds}};

    function launchBankingApp() {
      if (supportsAutofill) {
        window.location.href = primaryUrl;
        return;
      }
      if (isAndroid) {
        window.location.href = androidIntent;
        return;
      }
      if (isIos) {
        window.location.href = customScheme;
        return;
      }
      window.location.href = primaryUrl;
    }

    function copyText(val, label) {
      if (!navigator.clipboard) return;
      navigator.clipboard.writeText(val).then(function() {
        var t = document.getElementById('toast');
        t.textContent = 'Đã sao chép ' + label;
        t.classList.add('show');
        setTimeout(function() { t.classList.remove('show'); }, 1800);
      });
    }

    setInterval(function() {
      if (remaining > 0) {
        remaining--;
        var el = document.getElementById('countdown');
        if (el) el.textContent = remaining + 's';
      }
    }, 1000);

    {{autoOpenScript}}
  </script>
</body>
</html>
""";
    }

    /// <summary>
    /// Render trang HTML thông báo liên kết đã hết hạn hoặc không hợp lệ (HTTP 404/410).
    /// </summary>
    public static string RenderExpiredPageHtml() => """
<!DOCTYPE html>
<html lang="vi">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>Liên kết thanh toán đã hết hạn</title>
  <style>
    body {
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
      background: #f8fafc;
      color: #0f172a;
      min-height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      margin: 0;
      padding: 16px;
    }
    .box {
      background: #fff;
      max-width: 400px;
      width: 100%;
      padding: 28px 24px;
      border-radius: 20px;
      border: 1px solid #e2e8f0;
      text-align: center;
      box-shadow: 0 10px 25px -10px rgba(15, 23, 42, 0.08);
    }
    h1 { font-size: 18px; margin-bottom: 8px; color: #0f172a; }
    p { font-size: 13px; color: #64748b; line-height: 1.5; margin-bottom: 20px; }
    a {
      display: inline-block;
      background: #0f172a;
      color: #fff;
      text-decoration: none;
      font-size: 13px;
      font-weight: 600;
      padding: 10px 18px;
      border-radius: 10px;
    }
  </style>
</head>
<body>
  <div class="box">
    <h1>Liên kết thanh toán đã hết hạn</h1>
    <p>Phiên điều hướng VietQR Deeplink này không tồn tại hoặc đã quá thời gian hiệu lực. Vui lòng tạo lại mã thanh toán mới.</p>
    <a href="/">Quay về trang chủ</a>
  </div>
</body>
</html>
""";
}

