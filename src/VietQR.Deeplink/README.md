# VietQR.Deeplink — Engine Điều Hướng Thông Minh (Smart DeepLink Bridge & Intent Gateway)

Thư viện máy chủ (`Server Engine`) điều hướng mở ứng dụng ngân hàng và ví điện tử trên thiết bị di động (iOS, Android) và hiển thị trang thanh toán VietQR thông minh trên máy tính (Desktop).

---

## ⚡ Tính Năng Cốt Lõi

- **Hỗ trợ 44+ Ngân hàng & Ví điện tử tại Việt Nam (`DeeplinkAppCatalog`):**
  - **Nhóm 1 — Hỗ trợ Autofill (`SupportsAutofill = true`, 7 ứng dụng):** `MB`, `BIDV`, `OCB`, `TPB`, `CAKE`, `MOMO`, `ZALOPAY`. Mở thẳng màn hình chuyển khoản và tự động điền sẵn **Ngân hàng nhận + Số tài khoản + Số tiền + Nội dung**.
  - **Nhóm 2 — Mở trực tiếp ứng dụng (`SupportsAutofill = false`, 37+ ứng dụng):** `VCB`, `ICB`, `VBA`, `TCB`, `ACB`, `VPB`, `VIB`, `STB`, `HDB`, `SHB`, `LPB`, `MSB`, `SEAB`... Sử dụng `intent://` chuẩn trên Android và Custom Scheme trên iOS kèm fallback an toàn về App Store / Google Play và trang quét mã QR.
- **Tích hợp In-Process (Zero-Network-Hop) với `VietQR.Api`:**
  - Tự động kích hoạt cờ biên dịch `HAS_VIETQR_DEEPLINK` trong `VietQR.Api`.
  - Đăng ký `LocalVietQrDeeplinkClient` triển khai `IVietQrDeeplinkClient` chạy trực tiếp trong bộ nhớ RAM (không cần gọi HTTP ra máy chủ bên ngoài).
- **Quản lý phiên điều hướng ngắn hạn (`DeeplinkIntentStore`):**
  - Xác thực checksum CRC16 và giải mã chuỗi EMVCo bằng `VietQrParser.Parse`.
  - Sinh mã rút gọn `/p/{token}` Base62 ngẫu nhiên an toàn (`RandomNumberGenerator`), tự động thu hồi khi hết hạn (`IntentTtlSeconds`, mặc định 300 giây).
- **Trang Cầu Nối Thanh Toán Thông Minh (`GET /p/{token}`):**
  - Tự động nhận diện `User-Agent` (iOS, Android, Desktop).
  - Tự động kích hoạt mở ứng dụng ngân hàng trên thiết bị di động.
  - Hiển thị trực tiếp mã ảnh VietQR (`/p/{token}/qr.png`), đồng hồ đếm ngược thời gian hiệu lực, thông tin tài khoản thụ hưởng kèm nút **Sao chép nhanh** và **Tải ảnh mã QR**.

---

## 🔌 Tích Hợp Vào ASP.NET Core (`Program.cs`)

```csharp
using VietQR.Core;
using VietQR.Deeplink;

var builder = WebApplication.CreateBuilder(args);

// 1. Đăng ký VietQR.Core và VietQR.Deeplink Server Engine
builder.Services.AddVietQrCore();
builder.Services.AddDeeplinkServer(builder.Configuration);

var app = builder.Build();

// 2. Kích hoạt các Endpoint (/v1/apps, /v1/intents, /p/{token}, /p/{token}/qr.png)
app.UseDeeplinkServer();

app.Run();
```

---

## 🌐 Danh Sách Endpoint Được Cung Cấp

| Phương thức | Đường dẫn | Mô tả |
| :---: | :--- | :--- |
| `GET` | `/v1/apps` | Trả về danh sách 44+ ứng dụng ngân hàng/ví điện tử kèm cờ `supportsAutofill` và đường dẫn icon `/icons/{appCode}.png`. |
| `POST` | `/v1/intents` | Nhận `{ "appCode": "MB", "qrContent": "000201..." }`, kiểm tra CRC16 và trả về URL ngắn hạn `/p/{token}`. |
| `GET` | `/p/{token}` | Trang Smart Payment Bridge tự động mở App ngân hàng trên iOS/Android hoặc hiển thị thẻ thanh toán VietQR trên Desktop. Hỗ trợ `?mode=redirect` để chuyển hướng HTTP 302 trực tiếp. |
| `GET` | `/p/{token}/qr.png` | Trả về ảnh thẻ VietQR (PNG) của phiên giao dịch hiện tại (hỗ trợ `?download=1` để tải file). |

