# VietQR.Core.Net35 — Thư Viện VietQR (NAPAS247 / EMVCo) cho .NET Framework 3.5 (WinForms)

Thư viện **Zero-Dependency** (đóng gói toàn bộ trong **1 file `VietQR.Core.Net35.dll` duy nhất**) được thiết kế riêng cho các ứng dụng **Windows Forms chạy .NET Framework 3.5**.

---

## ✨ Đặc Điểm Nổi Bật

- **Chỉ 1 file `.dll` duy nhất (`VietQR.Core.Net35.dll`):** Không phụ thuộc `SkiaSharp`, `QRCoder`, `Newtonsoft.Json` hay bất kỳ thư viện C++ native bên ngoài nào. Chỉ cần `Add Reference` là chạy ngay.
- **Nhúng sẵn 100% tài nguyên (Offline):**
  - Danh mục **76+ ngân hàng** thành viên NAPAS (`banks.json`).
  - Toàn bộ **76 logo ngân hàng**, logo `VietQR`, `NAPAS247` và font `GoogleSans-Regular.ttf`.
  - Không gọi bất kỳ dịch vụ mạng (HTTP/Internet) nào.
- **Tương thích trực tiếp với WinForms (`System.Drawing` & `System.Windows.Forms`):**
  - Xuất trực tiếp đối tượng `System.Drawing.Bitmap` (`Image`) để gán thẳng vào `pictureBox.Image`.
  - Hỗ trợ đầy đủ 3 mẫu thẻ giống hệt `VietQR.Core`: `"compact"`, `"print"`, `"qr_only"`.
  - Hỗ trợ xuất `byte[]` (PNG, JPEG, BMP), `Stream`, chuỗi `Base64 Data URL`, và `Vector SVG`.
  - Tích hợp sẵn bộ khởi tạo nhanh **`VietQrGenerator`** và các model **`VietQrApiRequest` / `VietQrApiResponse`** để **thay thế trực tiếp 1-1 cho API `https://api.vietqr.io/v2/generate`** mà không cần đổi cấu trúc Form hiện tại.
  - Tự động nhận diện cả **mã BIN 6 số** (`"970422"`, `"970436"`) lẫn **mã/tên viết tắt ngân hàng** (`"MB"`, `"VCB"`, `"BIDV"`, `"Techcombank"`...).
  - Tích hợp sẵn Extension Methods cho `ComboBox` (`BindVietQrBanks`) và `PictureBox` (`ShowVietQr`).

---

## 📦 Đường Dẫn File `.dll` Đã Biên Dịch

Sau khi build, file `.dll` để import vào dự án WinForms .NET 3.5 nằm tại:
- **Bản Release (Khuyên dùng):** `src\VietQR.Core.Net35\bin\Release\net35\VietQR.Core.Net35.dll` (kèm `VietQR.Core.Net35.xml` hiển thị gợi ý code IntelliSense trong Visual Studio)
- **Bản Debug:** `src\VietQR.Core.Net35\bin\Debug\net35\VietQR.Core.Net35.dll`

### Cách tích hợp vào dự án WinForms .NET 3.5:
1. Mở dự án WinForms `.NET Framework 3.5` của bạn trong Visual Studio.
2. Chuột phải vào **References** $\rightarrow$ **Add Reference...** $\rightarrow$ tab **Browse**.
3. Chọn file `VietQR.Core.Net35.dll` (hoặc thêm trực tiếp project `src\VietQR.Core.Net35\VietQR.Core.Net35.csproj` vào Solution của bạn).
4. Khai báo `using VietQR.Core;` trong Form của bạn.

---

## 💻 Hướng Dẫn Sử Dụng Trong WinForms (.NET 3.5)

### Cách 1: Thay thế trực tiếp API `https://api.vietqr.io/v2/generate` trong Form `frmVieQR` (100% Offline)

Nếu dự án WinForms hiện tại của bạn có màn hình `frmVieQR` đang gọi `https://api.vietqr.io/v2/generate` qua `RestSharp` và `Newtonsoft.Json`, bạn có thể loại bỏ hoàn toàn phụ thuộc mạng/API bên ngoài bằng class **`VietQrGenerator`**.

#### Phương án 1A (Khuyên dùng): Tạo trực tiếp `Image` bằng `VietQrGenerator.GenerateImage(...)`
Phương án này ngắn gọn và nhanh nhất (< 5ms), không cần thông qua bước chuyển đổi trung gian Base64:

```csharp
using System;
using System.Drawing;
using System.Windows.Forms;
using VietQR.Core;

namespace HSBMS.VNPAY.PAYMENT
{
    public partial class frmVieQR : Form
    {
        string _orderId = "";
        decimal _money = 0;
        string _bankName = "";
        string _bankAccount = "";
        string _NDck = "";
        string _FullName = "";
        public Image QRImage { get; private set; }

        public frmVieQR(string orderId, decimal money, string BankName, string BankAccount, string NDck, string FullName)
        {
            InitializeComponent();
            _orderId = orderId;
            _money = money;
            _bankName = BankName;
            _bankAccount = BankAccount;
            _NDck = NDck;
            _FullName = FullName;
        }

        private void frmVieQR_Load(object sender, EventArgs e)
        {
            lblOrderId.Text = _orderId;
            lblMoney.Text = string.Format("{0:N0} VNĐ", _money);
            GenQR();
        }

        private void GenQR()
        {
            try
            {
                // Sử dụng _orderId hoặc _NDck làm nội dung chuyển khoản
                string addInfo = !string.IsNullOrEmpty(_orderId) ? _orderId : _NDck;

                // Tạo trực tiếp đối tượng Image (Bitmap) Offline 100%
                Image image = VietQrGenerator.GenerateImage(
                    _bankName,      // Hỗ trợ cả mã BIN 6 số ("970422") hoặc tên/mã viết tắt ("MB", "VCB", "BIDV"...)
                    _bankAccount,   // Số tài khoản nhận tiền
                    _money,         // Số tiền kiểu decimal (<= 0 sẽ tự tạo QR tĩnh)
                    addInfo,        // Nội dung chuyển khoản
                    _FullName,      // Tên chủ tài khoản hiển thị trên ảnh
                    "print"         // Mẫu thẻ: "print", "compact", hoặc "qr_only"
                );

                picQR.Image = image;
                picQR.SizeMode = PictureBoxSizeMode.Zoom;
                QRImage = image;
                btnDone.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi xử lý tạo mã QR: " + ex.Message);
            }
        }

        private void btnDone_Click_1(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void btnCancel_Click_1(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }
}
```

#### Phương án 1B: Giữ nguyên cấu trúc `ApiRequest` / `ApiResponse` và `Base64ToImage` như code cũ
Nếu muốn giữ nguyên cấu trúc truyền `apiRequest` và đọc `dataResult.data.qrDataURL` hoặc `dataResult.data.qrCode`:

```csharp
private void GenQR()
{
    try
    {
        var apiRequest = new VietQrApiRequest
        {
            acqId = _bankName,          // Mã BIN ("970422") hoặc mã ngân hàng ("MB", "VCB"...)
            accountNo = _bankAccount,
            accountName = _FullName,
            addInfo = _orderId,
            amount = _money,            // Nhận trực tiếp kiểu decimal
            format = "text",
            template = "print"
        };

        // Chạy 100% Offline thay cho RestClient("https://api.vietqr.io/v2/generate")
        VietQrApiResponse dataResult = VietQrGenerator.Generate(apiRequest);

        if (dataResult != null && dataResult.code == "00" && dataResult.data != null)
        {
            // Cách 1: Giải mã từ chuỗi data:image/png;base64,... giống code cũ
            Image image = VietQrGenerator.Base64ToImage(dataResult.data.qrDataURL);
            // Cách 2: Hoặc lấy trực tiếp ảnh đã render sẵn: Image image = dataResult.data.qrImage;

            picQR.Image = image;
            picQR.SizeMode = PictureBoxSizeMode.Zoom;
            QRImage = image;
            btnDone.Enabled = true;
        }
        else
        {
            MessageBox.Show("Không tạo được mã QR: " + (dataResult != null ? dataResult.desc : ""));
        }
    }
    catch (Exception ex)
    {
        MessageBox.Show("Lỗi kết nối hoặc xử lý: " + ex.Message);
    }
}
```

---

### Cách 2: Dùng Extension Methods cho `ComboBox` và `PictureBox`

```csharp
using System;
using System.Windows.Forms;
using VietQR.Core;

public partial class FrmThanhToan : Form
{
    public FrmThanhToan()
    {
        InitializeComponent();
    }

    private void FrmThanhToan_Load(object sender, EventArgs e)
    {
        // 1. Nạp danh sách 76+ ngân hàng vào ComboBox (tự chọn mặc định MBBank - 970422)
        cboNganHang.BindVietQrBanks("970422");
    }

    private void btnTaoQr_Click(object sender, EventArgs e)
    {
        string bankBin = cboNganHang.SelectedValue.ToString();
        string soTaiKhoan = txtSoTaiKhoan.Text.Trim();
        decimal soTien = decimal.Parse(txtSoTien.Text.Trim());
        string noiDung = txtNoiDung.Text.Trim();

        // 2. Tạo mã VietQR và hiển thị trực tiếp lên PictureBox (mẫu "compact", "print" hoặc "qr_only")
        string qrPayload = picVietQr.ShowVietQr(
            bankBinOrShortName: bankBin,
            accountNo: soTaiKhoan,
            amount: soTien,
            description: noiDung,
            accountName: "NGUYEN VAN A",
            template: "print" // Dùng "compact", "print" hoặc "qr_only"
        );

        txtChuoiEmv.Text = qrPayload;
    }
}
```

---

### Cách 3: Tạo chuỗi EMVCo và đối tượng `Bitmap` / Lưu file PNG

```csharp
using System.Drawing;
using VietQR.Core;

// 1. Tạo chuỗi EMVCo chuẩn NAPAS247 (QR Động hoặc Tĩnh)
string payload = VietQrPayload.CreateDynamic(
    BankBin.Create("970422"),     // Có thể truyền "970422" hoặc "MB"
    "056789799999",               // Số tài khoản
    amount: 150000,               // Số tiền VNĐ
    description: "Thanh toan HD123"
);

// 2. Render ra đối tượng System.Drawing.Bitmap để hiển thị hoặc in ấn
var imageService = new QrImageService();
Bitmap qrBitmap = imageService.RenderBitmap(payload, new QrRenderOptions
{
    Template = "compact",         // "compact", "print" hoặc "qr_only"
    BankBin = "970422",
    AccountNo = "056789799999",
    AccountName = "VO NGOC HIEN",
    Amount = 150000,
    Description = "Thanh toan HD123"
});

// Gán lên PictureBox
pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
pictureBox1.Image = qrBitmap;

// Hoặc lưu trực tiếp ra file ảnh (.png, .jpg, .bmp, .svg)
imageService.Save(@"D:\vietqr_print.png", payload, new QrRenderOptions
{
    Template = "print",
    BankBin = "970422",
    AccountNo = "056789799999",
    AccountName = "VO NGOC HIEN",
    Amount = 150000,
    Description = "Thanh toan HD123"
});
```

---

### Cách 4: Giải mã / Kiểm tra chuỗi EMVCo (`VietQrParser` & `VietQrDecoder`)

> **Lưu ý:** Tạo chuỗi EMVCo, phân tích chuỗi (`VietQrParser.Parse`) và vẽ ảnh thẻ QR là **100% Zero-Dependency** (chỉ cần duy nhất `VietQR.Core.Net35.dll`). Riêng tính năng tùy chọn đọc ngược mã QR từ file ảnh (`VietQrDecoder.Decode`) trên .NET 3.5 sẽ tự động nạp `zxing.dll` qua Reflection nếu bạn đặt `zxing.dll` cùng thư mục ứng dụng.

```csharp
using VietQR.Core;

Result<VietQrInfo> parsed = VietQrParser.Parse(payload);
if (parsed.IsSuccess)
{
    VietQrInfo info = parsed.Value;
    MessageBox.Show(string.Format(
        "Ngân hàng: {0}\nSố TK: {1}\nSố tiền: {2:N0} VNĐ\nNội dung: {3}\nCRC16 hợp lệ: {4}",
        info.BankInfo != null ? info.BankInfo.ShortName : info.BankBin,
        info.AccountNo,
        info.Amount,
        info.Description,
        info.IsValidCrc));
}
```

