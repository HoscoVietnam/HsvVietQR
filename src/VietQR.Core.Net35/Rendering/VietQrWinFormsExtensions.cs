using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace VietQR.Core
{
    /// <summary>
    /// Các hàm mở rộng tiện ích dành riêng cho ứng dụng Windows Forms (.NET Framework 3.5).
    /// Giúp nạp danh sách ngân hàng vào ComboBox và hiển thị mã VietQR lên PictureBox chỉ với 1 dòng lệnh.
    /// </summary>
    public static class VietQrWinFormsExtensions
    {
        /// <summary>
        /// Nạp danh mục 76+ ngân hàng NAPAS vào <see cref="ComboBox"/> của WinForms.
        /// Thiết lập sẵn <c>DisplayMember = "DisplayName"</c> và <c>ValueMember = "Bin"</c>.
        /// </summary>
        /// <param name="comboBox">ComboBox cần nạp danh sách ngân hàng.</param>
        /// <param name="defaultBankBinOrCode">Mã BIN hoặc tên viết tắt mặc định cần chọn (VD: "970422" hoặc "MB").</param>
        public static void BindVietQrBanks(this ComboBox comboBox, string defaultBankBinOrCode = "970422")
        {
            if (comboBox == null) throw new ArgumentNullException("comboBox");

            IList<BankInfo> banks = EmbeddedBankCatalog.Instance.GetAll();
            var bindingList = new List<BankInfo>(banks);

            comboBox.DisplayMember = "DisplayName";
            comboBox.ValueMember = "Bin";
            comboBox.DataSource = bindingList;

            if (!string.IsNullOrEmpty(defaultBankBinOrCode))
            {
                BankInfo def = EmbeddedBankCatalog.Instance.Find(defaultBankBinOrCode);
                if (def != null)
                {
                    comboBox.SelectedValue = def.Bin;
                }
            }
        }

        /// <summary>
        /// Tạo mã VietQR và hiển thị trực tiếp lên <see cref="PictureBox"/> trong WinForms (tự động giải phóng ảnh cũ nếu có).
        /// Trả về chuỗi EMVCo payload đã tạo.
        /// </summary>
        public static string ShowVietQr(
            this PictureBox pictureBox,
            string bankBin,
            string accountNo,
            long? amount = null,
            string description = null,
            string accountName = null,
            string template = "compact",
            VietQrServiceType serviceType = VietQrServiceType.Account,
            string billNumber = null)
        {
            if (pictureBox == null) throw new ArgumentNullException("pictureBox");

            string payload = VietQrPayload.Create(
                BankBin.Create(bankBin),
                accountNo,
                amount,
                description,
                serviceType,
                billNumber);

            var service = new QrImageService();
            Bitmap newBmp = service.RenderBitmap(payload, new QrRenderOptions
            {
                Template = template ?? "compact",
                BankBin = bankBin,
                AccountNo = accountNo,
                AccountName = accountName,
                Amount = amount,
                Description = description,
                BillNumber = billNumber,
                ServiceType = serviceType
            });

            Image oldImg = pictureBox.Image;
            pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox.Image = newBmp;
            if (oldImg != null)
            {
                oldImg.Dispose();
            }

            return payload;
        }

        /// <summary>
        /// Tạo mã VietQR từ số tiền kiểu <see cref="decimal"/> và hiển thị trực tiếp lên <see cref="PictureBox"/> trong WinForms.
        /// Trả về chuỗi EMVCo payload đã tạo.
        /// </summary>
        public static string ShowVietQr(
            this PictureBox pictureBox,
            string bankBinOrShortName,
            string accountNo,
            decimal amount,
            string description,
            string accountName,
            string template)
        {
            long? amountLong = amount > 0m ? (long?)Convert.ToInt64(Math.Round(amount, 0, MidpointRounding.AwayFromZero)) : null;
            return ShowVietQr(
                pictureBox,
                bankBinOrShortName,
                accountNo,
                amountLong,
                description,
                accountName,
                VietQrPayload.IsNullOrWhiteSpace(template) ? "print" : template);
        }
    }

    /// <summary>
    /// Mô hình Request tương thích 1-1 với <c>https://api.vietqr.io/v2/generate</c>.
    /// Dùng để thay thế trực tiếp class <c>ApiRequest</c> cũ mà không cần đổi cấu trúc dữ liệu.
    /// </summary>
    public sealed class VietQrApiRequest
    {
        /// <summary>Số tài khoản ngân hàng thụ hưởng.</summary>
        public string accountNo { get; set; }

        /// <summary>Tên chủ tài khoản hiển thị trên mã QR.</summary>
        public string accountName { get; set; }

        /// <summary>Mã BIN 6 số (VD: "970422") hoặc mã/tên viết tắt ngân hàng (VD: "MB", "VCB", "BIDV").</summary>
        public string acqId { get; set; }

        /// <summary>Số tiền chuyển khoản (VNĐ).</summary>
        public decimal amount { get; set; }

        /// <summary>Nội dung chuyển khoản (mã đơn hàng, ghi chú...).</summary>
        public string addInfo { get; set; }

        /// <summary>Định dạng kết quả (mặc định: "text").</summary>
        public string format { get; set; }

        /// <summary>Mẫu khung thẻ VietQR: "print" (mặc định), "compact", hoặc "qr_only".</summary>
        public string template { get; set; }

        public VietQrApiRequest()
        {
            format = "text";
            template = "print";
        }
    }

    /// <summary>
    /// Dữ liệu trả về bên trong <see cref="VietQrApiResponse"/>, tương thích 1-1 với <c>Data</c> của <c>api.vietqr.io/v2/generate</c>.
    /// </summary>
    public sealed class VietQrApiData
    {
        /// <summary>Chuỗi gốc EMVCo VietQR (Tag-Length-Value + CRC16).</summary>
        public string qrCode { get; set; }

        /// <summary>Chuỗi ảnh Base64 Data URL (<c>data:image/png;base64,...</c>).</summary>
        public string qrDataURL { get; set; }

        /// <summary>Đối tượng ảnh <see cref="Bitmap"/> đã kết xuất sẵn để gán trực tiếp vào <c>PictureBox.Image</c>.</summary>
        public Bitmap qrImage { get; set; }
    }

    /// <summary>
    /// Mô hình Response tương thích 1-1 với <c>https://api.vietqr.io/v2/generate</c>.
    /// Dùng để thay thế trực tiếp class <c>ApiResponse</c> cũ.
    /// </summary>
    public sealed class VietQrApiResponse
    {
        /// <summary>Mã trạng thái ("00" là thành công).</summary>
        public string code { get; set; }

        /// <summary>Mô tả trạng thái.</summary>
        public string desc { get; set; }

        /// <summary>Dữ liệu mã QR trả về (<see cref="VietQrApiData"/>).</summary>
        public VietQrApiData data { get; set; }
    }

    /// <summary>
    /// Bộ khởi tạo nhanh VietQR Offline (100% nội bộ, không cần mạng, không cần RestSharp/Newtonsoft.Json).
    /// Thiết kế chuyên biệt để thay thế trực tiếp lời gọi <c>https://api.vietqr.io/v2/generate</c> trong WinForms.
    /// </summary>
    public static class VietQrGenerator
    {
        private static readonly QrImageService _defaultService = new QrImageService();

        /// <summary>
        /// Tạo trực tiếp đối tượng ảnh <see cref="Bitmap"/> (kế thừa từ <see cref="Image"/>) chỉ với 1 lời gọi hàm.
        /// Phù hợp nhất để gán thẳng vào <c>picQR.Image</c> và <c>QRImage</c> trong WinForms.
        /// </summary>
        /// <param name="acqIdOrBankName">Mã BIN 6 số (VD: "970422") hoặc mã/tên ngân hàng (VD: "MB", "VCB", "BIDV").</param>
        /// <param name="accountNo">Số tài khoản nhận tiền.</param>
        /// <param name="amount">Số tiền chuyển khoản (VNĐ).</param>
        /// <param name="addInfo">Nội dung chuyển khoản (VD: mã đơn hàng).</param>
        /// <param name="accountName">Tên chủ tài khoản hiển thị trên ảnh QR.</param>
        /// <param name="template">Mẫu giao diện: "print" (mặc định), "compact", hoặc "qr_only".</param>
        public static Bitmap GenerateImage(
            string acqIdOrBankName,
            string accountNo,
            decimal amount,
            string addInfo,
            string accountName,
            string template)
        {
            return _defaultService.GenerateVietQrImage(
                acqIdOrBankName,
                accountNo,
                amount,
                addInfo,
                accountName,
                VietQrPayload.IsNullOrWhiteSpace(template) ? "print" : template);
        }

        /// <summary>
        /// Tạo trực tiếp đối tượng ảnh <see cref="Bitmap"/> với mẫu mặc định <c>"print"</c>.
        /// </summary>
        public static Bitmap GenerateImage(
            string acqIdOrBankName,
            string accountNo,
            decimal amount,
            string addInfo,
            string accountName)
        {
            return GenerateImage(acqIdOrBankName, accountNo, amount, addInfo, accountName, "print");
        }

        /// <summary>
        /// Tạo trực tiếp đối tượng ảnh <see cref="Bitmap"/> với mẫu mặc định <c>"print"</c> (không truyền tên chủ tài khoản).
        /// </summary>
        public static Bitmap GenerateImage(
            string acqIdOrBankName,
            string accountNo,
            decimal amount,
            string addInfo)
        {
            return GenerateImage(acqIdOrBankName, accountNo, amount, addInfo, null, "print");
        }

        /// <summary>
        /// Thay thế trực tiếp lời gọi API <c>https://api.vietqr.io/v2/generate</c> nhưng chạy 100% Offline trong bộ nhớ.
        /// Trả về <see cref="VietQrApiResponse"/> có đầy đủ <c>code = "00"</c>, <c>data.qrCode</c>, <c>data.qrDataURL</c> và <c>data.qrImage</c>.
        /// </summary>
        public static VietQrApiResponse Generate(VietQrApiRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request");
            }

            try
            {
                string tpl = VietQrPayload.IsNullOrWhiteSpace(request.template) ? "print" : request.template;
                string payload;
                Bitmap bmp = _defaultService.GenerateVietQrImage(
                    request.acqId,
                    request.accountNo,
                    request.amount,
                    request.addInfo,
                    request.accountName,
                    tpl,
                    out payload);

                string dataUrl;
                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    dataUrl = "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
                }

                return new VietQrApiResponse
                {
                    code = "00",
                    desc = "Gen VietQR successful! (Offline)",
                    data = new VietQrApiData
                    {
                        qrCode = payload,
                        qrDataURL = dataUrl,
                        qrImage = bmp
                    }
                };
            }
            catch (Exception ex)
            {
                return new VietQrApiResponse
                {
                    code = "99",
                    desc = ex.Message,
                    data = null
                };
            }
        }

        /// <summary>
        /// Thay thế trực tiếp lời gọi API <c>https://api.vietqr.io/v2/generate</c> bằng các tham số rời.
        /// </summary>
        public static VietQrApiResponse Generate(
            string acqIdOrBankName,
            string accountNo,
            decimal amount,
            string addInfo,
            string accountName,
            string template)
        {
            return Generate(new VietQrApiRequest
            {
                acqId = acqIdOrBankName,
                accountNo = accountNo,
                amount = amount,
                addInfo = addInfo,
                accountName = accountName,
                template = VietQrPayload.IsNullOrWhiteSpace(template) ? "print" : template
            });
        }

        /// <summary>
        /// Chuyển đổi chuỗi Base64 hoặc Data URL (<c>data:image/png;base64,...</c>) sang <see cref="Image"/>.
        /// </summary>
        public static Image Base64ToImage(string base64String)
        {
            return VietQrImageConverter.Base64ToImage(base64String);
        }
    }
}
