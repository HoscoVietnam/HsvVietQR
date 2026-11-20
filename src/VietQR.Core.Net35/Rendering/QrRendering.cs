using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace VietQR.Core
{
    /// <summary>
    /// Định dạng ảnh xuất ra của mã QR thanh toán VietQR.
    /// </summary>
    public enum QrImageFormat
    {
        /// <summary>Định dạng ảnh PNG tiêu chuẩn (Mặc định).</summary>
        Png = 0,

        /// <summary>Định dạng WebP (Trên GDI+ .NET 3.5 sẽ tự động xuất PNG không nén mất dữ liệu).</summary>
        Webp = 1,

        /// <summary>Định dạng Vector SVG.</summary>
        Svg = 2,

        /// <summary>Định dạng ảnh JPEG.</summary>
        Jpeg = 3,

        /// <summary>Định dạng ảnh Bitmap (BMP).</summary>
        Bmp = 4
    }

    /// <summary>
    /// Tùy chọn render thẻ QR (template compact/qr_only/print) và nhận diện thương hiệu.
    /// </summary>
    public class QrRenderOptions
    {
        public string Template { get; set; }
        public string BankBin { get; set; }
        public string BankName { get; set; }
        public string BankFullName { get; set; }
        public string BankLogoUrl { get; set; }
        public string AccountNo { get; set; }
        public string AccountName { get; set; }
        public long? Amount { get; set; }
        public string Description { get; set; }
        public string BillNumber { get; set; }
        public VietQrServiceType ServiceType { get; set; }
        public int Size { get; set; }
        public QrImageFormat Format { get; set; }
        public int Quality { get; set; }
        public byte[] CustomCenterLogo { get; set; }
        public float CenterLogoRatio { get; set; }

        public string CustomCenterLogoBase64
        {
            get
            {
                return CustomCenterLogo != null ? Convert.ToBase64String(CustomCenterLogo) : null;
            }
            set
            {
                if (VietQrPayload.IsNullOrWhiteSpace(value))
                {
                    CustomCenterLogo = null;
                    return;
                }
                string clean = value.Trim();
                int commaIdx = clean.IndexOf(',');
                if (commaIdx >= 0)
                {
                    clean = clean.Substring(commaIdx + 1);
                }
                CustomCenterLogo = Convert.FromBase64String(clean);
            }
        }

        public QrRenderOptions()
        {
            Template = "compact";
            ServiceType = VietQrServiceType.Account;
            Size = 500;
            Format = QrImageFormat.Png;
            Quality = 100;
            CenterLogoRatio = 0.18f;
        }
    }

    /// <summary>
    /// Dịch vụ tạo ảnh thẻ VietQR chuẩn nhận diện NAPAS247 trên nền tảng GDI+ (System.Drawing) của .NET Framework 3.5.
    /// Hỗ trợ xuất trực tiếp đối tượng Bitmap cho PictureBox WinForms, mảng byte PNG/JPEG/BMP, Stream hoặc SVG.
    /// </summary>
    public class QrImageService
    {
        private readonly IQrAssetResolver _assetResolver;
        private readonly IBankCatalog _bankCatalog;
        private readonly string _customRootPath;

        private const int MaxLogoCacheEntries = 128;
        private static readonly object CacheLock = new object();
        private static readonly Dictionary<string, byte[]> LogoCache = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        private static readonly object FontLock = new object();
        private static PrivateFontCollection _embeddedFonts;
        private static FontFamily _embeddedFontFamily;
        private static bool _fontInitialized;

        public QrImageService()
            : this(null, null, null)
        {
        }

        public QrImageService(
            IQrAssetResolver assetResolver = null,
            string customRootPath = null,
            IBankCatalog bankCatalog = null)
        {
            _assetResolver = assetResolver;
            _customRootPath = customRootPath;
            _bankCatalog = bankCatalog ?? EmbeddedBankCatalog.Instance;
        }

        public QrImageService(string customRootPath)
            : this(null, customRootPath, null)
        {
        }

        /// <summary>
        /// Tạo đối tượng <see cref="Bitmap"/> thẻ VietQR với cấu hình mặc định (mẫu compact).
        /// </summary>
        public Bitmap RenderBitmap(string emvQrContent)
        {
            return RenderBitmap(emvQrContent, null);
        }

        /// <summary>
        /// Tạo đối tượng <see cref="Bitmap"/> thẻ VietQR theo tùy chọn <see cref="QrRenderOptions"/>.
        /// Có thể gán trực tiếp vào <c>pictureBox.Image</c> trong WinForms.
        /// </summary>
        public Bitmap RenderBitmap(string emvQrContent, QrRenderOptions options)
        {
            if (VietQrPayload.IsNullOrWhiteSpace(emvQrContent))
            {
                throw new ArgumentException("Chuỗi nội dung mã QR (emvQrContent) không được để trống.", "emvQrContent");
            }

            var opt = options ?? new QrRenderOptions();
            if (string.Equals(opt.Template, "qr_only", StringComparison.OrdinalIgnoreCase))
            {
                return CreateQrOnlyBitmap(emvQrContent, opt);
            }

            int cw, ch;
            CalculateCardDimensions(opt, out cw, out ch);
            var cardBitmap = new Bitmap(cw, ch, PixelFormat.Format32bppArgb);
            cardBitmap.SetResolution(96f, 96f);

            using (var g = Graphics.FromImage(cardBitmap))
            {
                ConfigureHighQualityGraphics(g);
                RenderCardCanvas(g, cw, ch, emvQrContent, opt);
            }

            return cardBitmap;
        }

        /// <summary>
        /// Tạo đối tượng <see cref="Bitmap"/> chỉ chứa mã QR vuông 500x500px và icon VietQR ở tâm.
        /// </summary>
        public Bitmap RenderQrOnlyBitmap(string emvQrContent)
        {
            return RenderQrOnlyBitmap(emvQrContent, 500);
        }

        /// <summary>
        /// Tạo đối tượng <see cref="Bitmap"/> chỉ chứa mã QR vuông và icon VietQR ở tâm.
        /// </summary>
        public Bitmap RenderQrOnlyBitmap(string emvQrContent, int size)
        {
            return RenderBitmap(emvQrContent, new QrRenderOptions
            {
                Template = "qr_only",
                Size = size,
                Format = QrImageFormat.Png
            });
        }

        /// <summary>
        /// Render ảnh thẻ VietQR ra mảng byte PNG mặc định.
        /// </summary>
        public byte[] RenderBytes(string emvQrContent)
        {
            return RenderBytes(emvQrContent, null);
        }

        /// <summary>
        /// Render ảnh thẻ VietQR ra mảng byte theo định dạng cấu hình trong <see cref="QrRenderOptions"/> (PNG, JPEG, BMP, SVG).
        /// </summary>
        public byte[] RenderBytes(string emvQrContent, QrRenderOptions options)
        {
            var opt = options ?? new QrRenderOptions();
            using (var ms = new MemoryStream())
            {
                RenderToStream(emvQrContent, ms, opt);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Render ảnh thẻ VietQR trực tiếp ra luồng Stream với cấu hình mặc định.
        /// </summary>
        public void RenderToStream(string emvQrContent, Stream outputStream)
        {
            RenderToStream(emvQrContent, outputStream, null);
        }

        /// <summary>
        /// Render ảnh thẻ VietQR trực tiếp ra luồng Stream.
        /// </summary>
        public void RenderToStream(string emvQrContent, Stream outputStream, QrRenderOptions options)
        {
            if (VietQrPayload.IsNullOrWhiteSpace(emvQrContent))
            {
                throw new ArgumentException("Chuỗi nội dung mã QR (emvQrContent) không được để trống.", "emvQrContent");
            }
            if (outputStream == null)
            {
                throw new ArgumentNullException("outputStream");
            }

            var opt = options ?? new QrRenderOptions();
            if (opt.Format == QrImageFormat.Svg)
            {
                string svgXml = RenderSvg(emvQrContent, opt);
                byte[] svgBytes = Encoding.UTF8.GetBytes(svgXml);
                outputStream.Write(svgBytes, 0, svgBytes.Length);
                return;
            }

            using (var bmp = RenderBitmap(emvQrContent, opt))
            {
                SaveBitmapToStream(bmp, outputStream, opt.Format, opt.Quality);
            }
        }

        /// <summary>
        /// Render mã VietQR dạng chuỗi XML SVG với cấu hình mặc định.
        /// </summary>
        public string RenderSvg(string emvQrContent)
        {
            return RenderSvg(emvQrContent, null);
        }

        /// <summary>
        /// Render mã VietQR dạng chuỗi XML SVG.
        /// </summary>
        public string RenderSvg(string emvQrContent, QrRenderOptions options)
        {
            var opt = options ?? new QrRenderOptions();
            string centerLogoBase64 = null;

            if (opt.CustomCenterLogo != null && opt.CustomCenterLogo.Length > 0)
            {
                centerLogoBase64 = Convert.ToBase64String(opt.CustomCenterLogo);
            }
            else
            {
                byte[] defaultIconBytes = GetEmbeddedResourceBytes("VietQR.Core.Resources.vietqr_icon.png");
                if (defaultIconBytes != null && defaultIconBytes.Length > 0)
                {
                    centerLogoBase64 = Convert.ToBase64String(defaultIconBytes);
                }
            }

            return VietQrSvgRenderer.Render(
                emvQrContent,
                opt.Size > 0 ? opt.Size : 500,
                "#000000",
                "#FFFFFF",
                centerLogoBase64,
                opt.CenterLogoRatio);
        }

        /// <summary>
        /// Lưu trực tiếp ảnh thẻ VietQR ra file trên ổ đĩa với cấu hình mặc định.
        /// </summary>
        public void Save(string filePath, string emvQrContent)
        {
            Save(filePath, emvQrContent, null);
        }

        /// <summary>
        /// Lưu trực tiếp ảnh thẻ VietQR ra file trên ổ đĩa (tự động nhận diện đuôi .png, .jpg, .bmp, .svg).
        /// </summary>
        public void Save(string filePath, string emvQrContent, QrRenderOptions options)
        {
            if (VietQrPayload.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("Đường dẫn file không được để trống.", "filePath");
            }

            var opt = options ?? new QrRenderOptions();
            string ext = Path.GetExtension(filePath);
            if (!string.IsNullOrEmpty(ext))
            {
                ext = ext.ToLowerInvariant();
                if (ext == ".svg") opt.Format = QrImageFormat.Svg;
                else if (ext == ".jpg" || ext == ".jpeg") opt.Format = QrImageFormat.Jpeg;
                else if (ext == ".bmp") opt.Format = QrImageFormat.Bmp;
                else if (ext == ".png") opt.Format = QrImageFormat.Png;
            }

            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using (var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                RenderToStream(emvQrContent, fs, opt);
            }
        }

        /// <summary>
        /// Render ảnh thẻ VietQR chuẩn PNG và trả về mảng byte raw.
        /// </summary>
        public byte[] RenderPngBytes(
            string emvQrContent,
            string bankName = null,
            string bankBin = null,
            string bankLogoUrl = null,
            string template = "compact",
            bool qrOnly = false,
            string accountNo = null,
            string accountName = null,
            string amount = null,
            string description = null,
            string bankFullName = null,
            VietQrServiceType serviceType = VietQrServiceType.Account)
        {
            long? parsedAmt = ParseAmountNullable(amount);
            var opt = new QrRenderOptions
            {
                Template = qrOnly ? "qr_only" : (template ?? "compact"),
                BankBin = bankBin,
                BankName = bankName,
                BankFullName = bankFullName,
                BankLogoUrl = bankLogoUrl,
                AccountNo = accountNo,
                AccountName = accountName,
                Amount = parsedAmt,
                Description = description,
                ServiceType = serviceType,
                Format = QrImageFormat.Png
            };
            return RenderBytes(emvQrContent, opt);
        }

        /// <summary>
        /// Lưu ảnh thẻ VietQR thành file PNG trên ổ đĩa.
        /// </summary>
        public void SavePng(
            string filePath,
            string emvQrContent,
            string bankName = null,
            string bankBin = null,
            string bankLogoUrl = null,
            string template = "compact",
            bool qrOnly = false,
            string accountNo = null,
            string accountName = null,
            string amount = null,
            string description = null,
            string bankFullName = null,
            VietQrServiceType serviceType = VietQrServiceType.Account)
        {
            byte[] bytes = RenderPngBytes(
                emvQrContent, bankName, bankBin, bankLogoUrl, template, qrOnly,
                accountNo, accountName, amount, description, bankFullName, serviceType);
            VietQrImageConverter.SaveToFile(bytes, filePath);
        }

        /// <summary>
        /// Tạo ảnh thẻ VietQR và trả về chuỗi Data URL Base64.
        /// </summary>
        public string GenerateVietQrBase64(string emvQrContent, QrRenderOptions options = null)
        {
            if (VietQrPayload.IsNullOrWhiteSpace(emvQrContent))
            {
                throw new ArgumentException("Chuỗi nội dung mã QR (emvQrContent) không được để trống.", "emvQrContent");
            }

            var opt = options ?? new QrRenderOptions();
            string mime = GetMimeType(opt.Format);
            byte[] bytes = RenderBytes(emvQrContent, opt);
            return string.Format("data:{0};base64,{1}", mime, Convert.ToBase64String(bytes));
        }

        public string GenerateVietQrBase64(
            string emvQrContent,
            string bankName = null,
            string bankBin = null,
            string bankLogoUrl = null,
            string template = "compact",
            bool qrOnly = false,
            string accountNo = null,
            string accountName = null,
            string amount = null,
            string description = null,
            string bankFullName = null,
            VietQrServiceType serviceType = VietQrServiceType.Account)
        {
            long? parsedAmt = ParseAmountNullable(amount);
            var opt = new QrRenderOptions
            {
                Template = qrOnly ? "qr_only" : (template ?? "compact"),
                BankBin = bankBin,
                BankName = bankName,
                BankFullName = bankFullName,
                BankLogoUrl = bankLogoUrl,
                AccountNo = accountNo,
                AccountName = accountName,
                Amount = parsedAmt,
                Description = description,
                ServiceType = serviceType,
                Format = QrImageFormat.Png
            };
            return GenerateVietQrBase64(emvQrContent, opt);
        }

        public string GenerateQrOnlyBase64(string emvQrContent, int size = 500)
        {
            byte[] bytes = GenerateQrOnlyBytes(emvQrContent, size);
            return "data:image/png;base64," + Convert.ToBase64String(bytes);
        }

        public byte[] GenerateQrOnlyBytes(string emvQrContent, int size = 500)
        {
            return GenerateQrOnlyBytes(emvQrContent, new QrRenderOptions
            {
                Template = "qr_only",
                Size = size,
                Format = QrImageFormat.Png
            });
        }

        public byte[] GenerateQrOnlyBytes(string emvQrContent, QrRenderOptions options)
        {
            var opt = options ?? new QrRenderOptions { Template = "qr_only" };
            opt.Template = "qr_only";
            return RenderBytes(emvQrContent, opt);
        }

        /// <summary>
        /// Tạo trực tiếp ảnh <see cref="Bitmap"/> thẻ VietQR chỉ trong 1 bước từ thông tin tài khoản, số tiền và nội dung chuyển khoản
        /// (không cần gọi tách riêng <c>VietQrPayload.Create</c>).
        /// </summary>
        /// <param name="acqIdOrBankName">Mã BIN 6 số (VD: "970422") hoặc mã/tên viết tắt ngân hàng (VD: "MB", "VCB", "BIDV").</param>
        /// <param name="accountNo">Số tài khoản hoặc số thẻ thụ hưởng.</param>
        /// <param name="amount">Số tiền chuyển khoản (VNĐ). Truyền &lt;= 0 để tạo QR tĩnh.</param>
        /// <param name="addInfo">Nội dung chuyển khoản.</param>
        /// <param name="accountName">Tên chủ tài khoản hiển thị trên ảnh thẻ VietQR.</param>
        /// <param name="template">Mẫu giao diện: "print" (mặc định giống api.vietqr.io), "compact", hoặc "qr_only".</param>
        public Bitmap GenerateVietQrImage(
            string acqIdOrBankName,
            string accountNo,
            decimal amount,
            string addInfo,
            string accountName,
            string template)
        {
            string payload;
            return GenerateVietQrImage(acqIdOrBankName, accountNo, amount, addInfo, accountName, template, out payload);
        }

        /// <summary>
        /// Tạo trực tiếp ảnh <see cref="Bitmap"/> thẻ VietQR chỉ trong 1 bước và trả kèm chuỗi EMVCo QR (<paramref name="emvPayload"/>).
        /// </summary>
        public Bitmap GenerateVietQrImage(
            string acqIdOrBankName,
            string accountNo,
            decimal amount,
            string addInfo,
            string accountName,
            string template,
            out string emvPayload)
        {
            long? amountLong = amount > 0m ? (long?)Convert.ToInt64(Math.Round(amount, 0, MidpointRounding.AwayFromZero)) : null;
            BankBin bin = BankBin.Create(acqIdOrBankName);

            var qr = new VietQr()
                .SetBeneficiaryOrganization(bin.Value, accountNo);

            if (amountLong.HasValue && amountLong.Value > 0)
            {
                qr.SetTransactionAmount(amountLong.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (!VietQrPayload.IsNullOrWhiteSpace(addInfo))
            {
                qr.SetAdditionalData(purpose: addInfo);
            }

            if (!VietQrPayload.IsNullOrWhiteSpace(accountName))
            {
                qr.SetMerchantInfo(accountName);
            }

            emvPayload = qr.Build();

            return RenderBitmap(emvPayload, new QrRenderOptions
            {
                Template = VietQrPayload.IsNullOrWhiteSpace(template) ? "print" : template,
                BankBin = bin.Value,
                AccountNo = accountNo,
                AccountName = accountName,
                Amount = amountLong,
                Description = addInfo,
                Format = QrImageFormat.Png
            });
        }

        private static long? ParseAmountNullable(string amount)
        {
            if (VietQrPayload.IsNullOrWhiteSpace(amount)) return null;
            string cleaned = amount.Replace(",", string.Empty).Replace(".", string.Empty).Trim();
            long val;
            if (long.TryParse(cleaned, NumberStyles.Integer, CultureInfo.InvariantCulture, out val))
            {
                return val;
            }
            return null;
        }

        private static string GetMimeType(QrImageFormat format)
        {
            switch (format)
            {
                case QrImageFormat.Svg: return "image/svg+xml";
                case QrImageFormat.Jpeg: return "image/jpeg";
                case QrImageFormat.Bmp: return "image/bmp";
                default: return "image/png";
            }
        }

        private static void SaveBitmapToStream(Bitmap bmp, Stream stream, QrImageFormat format, int quality)
        {
            if (format == QrImageFormat.Bmp)
            {
                bmp.Save(stream, ImageFormat.Bmp);
                return;
            }

            if (format == QrImageFormat.Jpeg)
            {
                int q = Math.Max(1, Math.Min(100, quality));
                ImageCodecInfo jpgEncoder = GetEncoder(ImageFormat.Jpeg);
                if (jpgEncoder != null)
                {
                    using (var encParams = new EncoderParameters(1))
                    {
                        encParams.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)q);
                        bmp.Save(stream, jpgEncoder, encParams);
                        return;
                    }
                }
                bmp.Save(stream, ImageFormat.Jpeg);
                return;
            }

            bmp.Save(stream, ImageFormat.Png);
        }

        private static ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            for (int i = 0; i < codecs.Length; i++)
            {
                if (codecs[i].FormatID == format.Guid)
                {
                    return codecs[i];
                }
            }
            return null;
        }

        private void HydrateBankMetadata(
            QrRenderOptions options,
            string emvQrContent,
            out string bankBin,
            out string bankName,
            out string bankFullName,
            out string bankLogoUrl)
        {
            bankBin = options.BankBin;
            bankName = options.BankName;
            bankFullName = options.BankFullName;
            bankLogoUrl = options.BankLogoUrl;

            if (string.IsNullOrEmpty(bankBin) && string.IsNullOrEmpty(bankName) && !string.IsNullOrEmpty(emvQrContent))
            {
                string extractedBin = VietQr.ExtractBinFromEmvCo(emvQrContent);
                if (!string.IsNullOrEmpty(extractedBin))
                {
                    bankBin = extractedBin;
                }
            }

            string lookupKey = !string.IsNullOrEmpty(bankBin) ? bankBin : bankName;
            if (!string.IsNullOrEmpty(lookupKey))
            {
                var bankInfo = _bankCatalog.Find(lookupKey);
                if (bankInfo != null)
                {
                    if (bankBin == null) bankBin = bankInfo.Bin;
                    if (bankName == null) bankName = bankInfo.ShortName;
                    if (bankFullName == null) bankFullName = bankInfo.Name;
                    if (bankLogoUrl == null) bankLogoUrl = bankInfo.Logo;
                }
            }
        }

        private void CalculateCardDimensions(QrRenderOptions options, out int width, out int height)
        {
            bool isPrint = string.Equals(options.Template, "print", StringComparison.OrdinalIgnoreCase);
            if (!isPrint)
            {
                width = 540;
                height = 540;
                return;
            }

            string bankBin, bankName, bankFullName, bankLogoUrl;
            HydrateBankMetadata(options, null, out bankBin, out bankName, out bankFullName, out bankLogoUrl);

            int lines = 0;
            if (options.Amount.HasValue && options.Amount.Value > 0) lines++;
            if (!VietQrPayload.IsNullOrWhiteSpace(options.Description)) lines++;
            if (!VietQrPayload.IsNullOrWhiteSpace(options.AccountName)) lines++;
            if (!VietQrPayload.IsNullOrWhiteSpace(options.AccountNo)) lines++;
            string displayBankName = !VietQrPayload.IsNullOrWhiteSpace(bankFullName) ? bankFullName.Trim() : (bankName ?? string.Empty);
            if (!VietQrPayload.IsNullOrWhiteSpace(displayBankName)) lines++;

            width = 600;
            height = 590 + (lines * 28) + 16;
        }

        private struct PrintLineInfo
        {
            public string Text;
            public bool IsBold;
            public float FontSizePx;

            public PrintLineInfo(string text, bool isBold, float fontSizePx)
            {
                Text = text;
                IsBold = isBold;
                FontSizePx = fontSizePx;
            }
        }

        private void RenderCardCanvas(
            Graphics g,
            int canvasWidth,
            int canvasHeight,
            string emvQrContent,
            QrRenderOptions options)
        {
            string bankBin, bankName, bankFullName, bankLogoUrl;
            HydrateBankMetadata(options, emvQrContent, out bankBin, out bankName, out bankFullName, out bankLogoUrl);

            string template = options.Template;
            string accountNo = options.AccountNo;
            string accountName = options.AccountName;
            string description = options.Description;
            VietQrServiceType serviceType = options.ServiceType;

            bool isPrint = string.Equals(template, "print", StringComparison.OrdinalIgnoreCase);
            var printLines = new List<PrintLineInfo>();

            if (isPrint)
            {
                if (options.Amount.HasValue && options.Amount.Value > 0)
                {
                    string formattedAmount = FormatAmountVnd(options.Amount.Value.ToString(CultureInfo.InvariantCulture));
                    printLines.Add(new PrintLineInfo("Số tiền: " + formattedAmount, false, 16f));
                }

                if (!VietQrPayload.IsNullOrWhiteSpace(description))
                {
                    printLines.Add(new PrintLineInfo("Nội dung CK: " + description.Trim(), false, 16f));
                }

                if (!VietQrPayload.IsNullOrWhiteSpace(accountName))
                {
                    printLines.Add(new PrintLineInfo("Tên chủ TK: " + accountName.Trim().ToUpperInvariant(), false, 16f));
                }

                if (!VietQrPayload.IsNullOrWhiteSpace(accountNo))
                {
                    string label = serviceType == VietQrServiceType.Card ? "Số thẻ" : "Số TK";
                    printLines.Add(new PrintLineInfo(label + ": " + accountNo.Trim(), true, 18f));
                }

                string displayBankName = !VietQrPayload.IsNullOrWhiteSpace(bankFullName) ? bankFullName.Trim() : (bankName ?? string.Empty);
                if (!VietQrPayload.IsNullOrWhiteSpace(displayBankName))
                {
                    printLines.Add(new PrintLineInfo(displayBankName, false, 15f));
                }
            }

            bool[][] matrix = QrCodeMatrixGenerator.EncodeText(emvQrContent, QrEccLevel.Q);

            g.Clear(Color.White);

            // 1. Header: Logo VietQR
            using (Bitmap vietQrBmp = LoadVietQrLogoBitmap())
            {
                if (vietQrBmp != null)
                {
                    RectangleF srcRect;
                    float unusedCenterY;
                    GetVisualBounds(vietQrBmp, out srcRect, out unusedCenterY);

                    int targetH = isPrint ? 48 : 46;
                    int targetW = (int)(targetH * (srcRect.Width / Math.Max(1f, srcRect.Height)));
                    int logoX = (canvasWidth - targetW) / 2;
                    int logoY = isPrint ? 24 : 18;

                    g.DrawImage(vietQrBmp, new RectangleF(logoX, logoY, targetW, targetH), srcRect, GraphicsUnit.Pixel);
                }
            }

            // 2. Khung viền & Mã QR
            int qrFrameSize = 374;
            int frameX = (canvasWidth - qrFrameSize) / 2;
            int frameY = isPrint ? 96 : 84;

            using (var borderPen = new Pen(Color.FromArgb(0, 59, 112), 2.5f))
            {
                g.DrawRectangle(borderPen, frameX, frameY, qrFrameSize, qrFrameSize);
            }

            int padding = 9;
            int innerQrSize = qrFrameSize - (padding * 2);
            int innerQrX = frameX + padding;
            int innerQrY = frameY + padding;

            int activeModuleCount = matrix.Length;
            float moduleSize = (float)innerQrSize / activeModuleCount;

            var prevSmoothing = g.SmoothingMode;
            var prevPixelOffset = g.PixelOffsetMode;
            g.SmoothingMode = SmoothingMode.None;
            g.PixelOffsetMode = PixelOffsetMode.Half;

            for (int row = 0; row < activeModuleCount; row++)
            {
                bool[] rowModules = matrix[row];
                for (int col = 0; col < activeModuleCount; col++)
                {
                    if (rowModules[col])
                    {
                        float l = innerQrX + (col * moduleSize);
                        float t = innerQrY + (row * moduleSize);
                        float r = innerQrX + ((col + 1) * moduleSize);
                        float b = innerQrY + ((row + 1) * moduleSize);
                        g.FillRectangle(Brushes.Black, l, t, r - l, b - t);
                    }
                }
            }

            g.SmoothingMode = prevSmoothing;
            g.PixelOffsetMode = prevPixelOffset;

            // 3. Icon trung tâm QR
            using (Bitmap centerIconBmp = LoadCustomOrDefaultCenterIcon(options.CustomCenterLogo))
            {
                if (centerIconBmp != null)
                {
                    int boxSize = 58;
                    int boxX = (canvasWidth - boxSize) / 2;
                    int boxY = frameY + (qrFrameSize - boxSize) / 2;

                    using (GraphicsPath roundPath = CreateRoundedRectPath(new RectangleF(boxX, boxY, boxSize, boxSize), 5f))
                    {
                        g.FillPath(Brushes.White, roundPath);
                    }

                    int iconSize = 46;
                    int iconX = (canvasWidth - iconSize) / 2;
                    int iconY = frameY + (qrFrameSize - iconSize) / 2;

                    g.DrawImage(centerIconBmp, new RectangleF(iconX, iconY, iconSize, iconSize));
                }
            }

            // 4. Footer: Napas247 | Ngân hàng
            int footerCenterY = isPrint ? 516 : 495;
            int targetVisualHeight = 35;

            using (Bitmap napasBmp = LoadNapasLogoBitmap())
            using (Bitmap bankBmp = GetBankLogoBitmap(bankBin, bankLogoUrl, bankName))
            {
                int napasW = 0, napasH = 0;
                RectangleF napasSrcRect = RectangleF.Empty;
                float napasCenterY = 0f;
                float scale = 1f;

                if (napasBmp != null)
                {
                    GetVisualBounds(napasBmp, out napasSrcRect, out napasCenterY);
                    scale = (float)targetVisualHeight / Math.Max(1f, napasSrcRect.Height);
                    napasW = (int)(napasSrcRect.Width * scale);
                    napasH = (int)(napasSrcRect.Height * scale);
                }

                int bankW = 0, bankH = 0;
                RectangleF bankSrcRect = RectangleF.Empty;
                float bankCenterY = 0f;
                float bankScale = scale;
                SizeF bankTextSize = SizeF.Empty;

                using (Font fallbackFont = CreatePreferredFont(24f, FontStyle.Bold))
                {
                    if (bankBmp != null)
                    {
                        GetVisualBounds(bankBmp, out bankSrcRect, out bankCenterY);
                        bankScale = (float)targetVisualHeight / Math.Max(1f, bankSrcRect.Height);
                        if (bankBmp.Width >= 800 && bankBmp.Height >= 300)
                        {
                            bankScale = scale;
                        }
                        if (bankSrcRect.Height * bankScale > 42f)
                        {
                            bankScale = 42f / bankSrcRect.Height;
                        }
                        if (bankSrcRect.Width * bankScale > 220f)
                        {
                            bankScale = 220f / bankSrcRect.Width;
                        }

                        bankW = (int)(bankSrcRect.Width * bankScale);
                        bankH = (int)(bankSrcRect.Height * bankScale);
                    }
                    else if (!VietQrPayload.IsNullOrWhiteSpace(bankName))
                    {
                        bankTextSize = g.MeasureString(bankName, fallbackFont);
                        bankW = (int)bankTextSize.Width;
                        bankH = (int)bankTextSize.Height;
                    }

                    int gap = 14;
                    int lineWidth = 2;
                    int totalGroupWidth = napasW + gap + lineWidth + gap + bankW;
                    int groupStartX = (canvasWidth - totalGroupWidth) / 2;

                    int napasX = groupStartX;
                    int sepX = napasX + napasW + gap + (lineWidth / 2);
                    int bankX = sepX + (lineWidth / 2) + gap;

                    using (var linePen = new Pen(Color.FromArgb(0, 59, 112), lineWidth))
                    {
                        g.DrawLine(linePen, sepX, footerCenterY - 18, sepX, footerCenterY + 18);
                    }

                    if (napasBmp != null)
                    {
                        float napasVisualOffset = napasCenterY - napasSrcRect.Top;
                        int napasY = (int)Math.Round(footerCenterY - (napasVisualOffset * scale));
                        g.DrawImage(napasBmp, new RectangleF(napasX, napasY, napasW, napasH), napasSrcRect, GraphicsUnit.Pixel);
                    }

                    if (bankBmp != null)
                    {
                        float bankVisualOffset = bankCenterY - bankSrcRect.Top;
                        int bY = (int)Math.Round(footerCenterY - (bankVisualOffset * bankScale));
                        g.DrawImage(bankBmp, new RectangleF(bankX, bY, bankW, bankH), bankSrcRect, GraphicsUnit.Pixel);
                    }
                    else if (!VietQrPayload.IsNullOrWhiteSpace(bankName))
                    {
                        using (var fallbackBrush = new SolidBrush(Color.FromArgb(0, 82, 155)))
                        {
                            float textY = footerCenterY - (bankTextSize.Height / 2f);
                            g.DrawString(bankName, fallbackFont, fallbackBrush, bankX, textY);
                        }
                    }
                }
            }

            // 5. Dòng thông tin in ấn cho mẫu "print"
            if (isPrint && printLines.Count > 0)
            {
                using (var textBrush = new SolidBrush(Color.FromArgb(0, 59, 112)))
                using (var sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Near;

                    float currentY = 550f;
                    for (int i = 0; i < printLines.Count; i++)
                    {
                        PrintLineInfo line = printLines[i];
                        using (Font font = CreatePreferredFont(line.FontSizePx, line.IsBold ? FontStyle.Bold : FontStyle.Regular))
                        {
                            g.DrawString(line.Text, font, textBrush, new PointF(canvasWidth / 2f, currentY), sf);
                            currentY += line.IsBold ? 29f : 26f;
                        }
                    }
                }
            }
        }

        private Bitmap CreateQrOnlyBitmap(string emvQrContent, QrRenderOptions options)
        {
            int size = options.Size > 0 ? options.Size : 500;
            bool[][] matrix = QrCodeMatrixGenerator.EncodeText(emvQrContent, QrEccLevel.Q);

            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            bmp.SetResolution(96f, 96f);

            using (var g = Graphics.FromImage(bmp))
            {
                ConfigureHighQualityGraphics(g);
                g.Clear(Color.White);

                int padding = Math.Max(10, (int)(size * 0.02));
                int innerQrSize = size - (padding * 2);
                int activeModuleCount = matrix.Length;
                float moduleSize = (float)innerQrSize / activeModuleCount;

                var prevSmoothing = g.SmoothingMode;
                var prevPixelOffset = g.PixelOffsetMode;
                g.SmoothingMode = SmoothingMode.None;
                g.PixelOffsetMode = PixelOffsetMode.Half;

                for (int row = 0; row < activeModuleCount; row++)
                {
                    bool[] rowModules = matrix[row];
                    for (int col = 0; col < activeModuleCount; col++)
                    {
                        if (rowModules[col])
                        {
                            float l = padding + (col * moduleSize);
                            float t = padding + (row * moduleSize);
                            float r = padding + ((col + 1) * moduleSize);
                            float b = padding + ((row + 1) * moduleSize);
                            g.FillRectangle(Brushes.Black, l, t, r - l, b - t);
                        }
                    }
                }

                g.SmoothingMode = prevSmoothing;
                g.PixelOffsetMode = prevPixelOffset;

                using (Bitmap centerIconBmp = LoadCustomOrDefaultCenterIcon(options.CustomCenterLogo))
                {
                    if (centerIconBmp != null)
                    {
                        float ratio = (options.CenterLogoRatio > 0f && options.CenterLogoRatio <= 0.35f)
                            ? options.CenterLogoRatio
                            : 0.18f;

                        int boxSize = (int)(innerQrSize * ratio);
                        int boxX = (size - boxSize) / 2;
                        int boxY = (size - boxSize) / 2;
                        float cornerRadius = Math.Max(4f, (int)(boxSize * 0.12));

                        using (GraphicsPath roundPath = CreateRoundedRectPath(new RectangleF(boxX, boxY, boxSize, boxSize), cornerRadius))
                        {
                            g.FillPath(Brushes.White, roundPath);
                        }

                        int iconSize = (int)(boxSize * 0.82);
                        int iconX = (size - iconSize) / 2;
                        int iconY = (size - iconSize) / 2;

                        g.DrawImage(centerIconBmp, new RectangleF(iconX, iconY, iconSize, iconSize));
                    }
                }
            }

            return bmp;
        }

        private static void ConfigureHighQualityGraphics(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        }

        private static GraphicsPath CreateRoundedRectPath(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2f;
            if (d <= 0f || d > rect.Width || d > rect.Height)
            {
                path.AddRectangle(rect);
                return path;
            }

            path.AddArc(rect.X, rect.Y, d, d, 180f, 90f);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270f, 90f);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0f, 90f);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90f, 90f);
            path.CloseFigure();
            return path;
        }

        private Bitmap LoadCustomOrDefaultCenterIcon(byte[] customLogoBytes)
        {
            if (customLogoBytes != null && customLogoBytes.Length > 0)
            {
                return DecodeBitmapFromBytes(customLogoBytes);
            }
            return LoadCenterIconBitmap();
        }

        private Bitmap GetBankLogoBitmap(string bankBin, string bankLogoUrl, string bankName)
        {
            string cleanBin = bankBin != null ? bankBin.Trim() : null;
            string cleanName = bankName != null ? bankName.Trim() : null;
            string cacheKey = !string.IsNullOrEmpty(cleanBin)
                ? cleanBin
                : (!string.IsNullOrEmpty(cleanName) ? cleanName : (bankLogoUrl != null ? bankLogoUrl.Trim() : string.Empty));

            if (!string.IsNullOrEmpty(cacheKey))
            {
                byte[] cachedBytes;
                lock (CacheLock)
                {
                    if (LogoCache.TryGetValue(cacheKey, out cachedBytes))
                    {
                        return DecodeBitmapFromBytes(cachedBytes);
                    }
                }
            }

            // 1. Đọc từ Embedded Resources theo mã BIN (chỉ cho phép chữ số để an toàn)
            if (!string.IsNullOrEmpty(cleanBin) && IsSafeIdentifier(cleanBin))
            {
                byte[] embBytes = GetEmbeddedResourceBytes("VietQR.Core.Resources.logos." + cleanBin + ".png");
                if (embBytes != null && embBytes.Length > 0)
                {
                    CacheLogo(cacheKey, embBytes);
                    return DecodeBitmapFromBytes(embBytes);
                }
            }

            // 2. Tra cứu mã BIN qua Catalog bộ nhớ
            BankInfo bankInfo = _bankCatalog.Find(cleanBin) ?? _bankCatalog.Find(cleanName);
            if (bankInfo != null && !string.IsNullOrEmpty(bankInfo.Bin))
            {
                byte[] embBytes = GetEmbeddedResourceBytes("VietQR.Core.Resources.logos." + bankInfo.Bin + ".png");
                if (embBytes != null && embBytes.Length > 0)
                {
                    CacheLogo(cacheKey, embBytes);
                    return DecodeBitmapFromBytes(embBytes);
                }
            }

            // 3. Fallback đọc từ customRootPath nếu có cấu hình và cleanBin an toàn (chống Path Traversal)
            if (!string.IsNullOrEmpty(_customRootPath) && !string.IsNullOrEmpty(cleanBin) && IsSafeIdentifier(cleanBin))
            {
                string[] candidates = new string[]
                {
                    Path.Combine(_customRootPath, cleanBin + ".png"),
                    Path.Combine(Path.Combine(_customRootPath, "logos"), cleanBin + ".png")
                };

                for (int i = 0; i < candidates.Length; i++)
                {
                    if (File.Exists(candidates[i]))
                    {
                        try
                        {
                            byte[] bytes = File.ReadAllBytes(candidates[i]);
                            CacheLogo(cacheKey, bytes);
                            return DecodeBitmapFromBytes(bytes);
                        }
                        catch
                        {
                        }
                    }
                }
            }

            return null;
        }

        private static bool IsSafeIdentifier(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-') return false;
            }
            return true;
        }

        private static void CacheLogo(string key, byte[] bytes)
        {
            if (string.IsNullOrEmpty(key) || bytes == null) return;
            lock (CacheLock)
            {
                if (LogoCache.Count >= MaxLogoCacheEntries)
                {
                    LogoCache.Clear();
                }
                LogoCache[key] = bytes;
            }
        }

        private static void GetVisualBounds(Bitmap bitmap, out RectangleF bounds, out float centerY)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;
            int minX = width, maxX = 0;
            int minY = height, maxY = 0;
            bool found = false;
            double weightedY = 0;
            long totalWeight = 0;

            BitmapData bmpData = null;
            try
            {
                bmpData = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                int stride = bmpData.Stride;
                int totalBytes = Math.Abs(stride) * height;
                byte[] buffer = new byte[totalBytes];
                Marshal.Copy(bmpData.Scan0, buffer, 0, totalBytes);

                for (int y = 0; y < height; y++)
                {
                    int rowOffset = y * stride;
                    for (int x = 0; x < width; x++)
                    {
                        int offset = rowOffset + (x * 4);
                        byte b = buffer[offset];
                        byte g = buffer[offset + 1];
                        byte r = buffer[offset + 2];
                        byte a = buffer[offset + 3];

                        bool isBackground = a <= 15 || (r >= 248 && g >= 248 && b >= 248);
                        if (!isBackground)
                        {
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                            weightedY += y;
                            totalWeight++;
                            found = true;
                        }
                    }
                }
            }
            finally
            {
                if (bmpData != null)
                {
                    bitmap.UnlockBits(bmpData);
                }
            }

            if (!found)
            {
                bounds = new RectangleF(0, 0, width, height);
                centerY = height / 2f;
                return;
            }

            centerY = totalWeight > 0 ? (float)(weightedY / totalWeight) : (minY + maxY) / 2f;
            bounds = new RectangleF(minX, minY, (maxX - minX) + 1, (maxY - minY) + 1);
        }

        private static byte[] GetEmbeddedResourceBytes(string resourceName)
        {
            try
            {
                var asm = typeof(QrImageService).Assembly;
                using (var stream = asm.GetManifestResourceStream(resourceName))
                {
                    if (stream == null) return null;
                    using (var ms = new MemoryStream())
                    {
                        byte[] buf = new byte[4096];
                        int read;
                        while ((read = stream.Read(buf, 0, buf.Length)) > 0)
                        {
                            ms.Write(buf, 0, read);
                        }
                        return ms.ToArray();
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        private static Bitmap DecodeBitmapFromBytes(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return null;
            try
            {
                using (var ms = new MemoryStream(bytes))
                using (var temp = new Bitmap(ms))
                {
                    return new Bitmap(temp);
                }
            }
            catch
            {
                return null;
            }
        }

        private Bitmap LoadVietQrLogoBitmap()
        {
            var emb = DecodeBitmapFromBytes(GetEmbeddedResourceBytes("VietQR.Core.Resources.vietqr.png"));
            if (emb != null) return emb;
            string path = GetDataFilePath("vietqr.png");
            return File.Exists(path) ? DecodeBitmapFromBytes(File.ReadAllBytes(path)) : null;
        }

        private Bitmap LoadCenterIconBitmap()
        {
            var emb = DecodeBitmapFromBytes(GetEmbeddedResourceBytes("VietQR.Core.Resources.vietqr_icon.png"));
            if (emb != null) return emb;
            string path = GetDataFilePath("vietqr_icon.png");
            return File.Exists(path) ? DecodeBitmapFromBytes(File.ReadAllBytes(path)) : null;
        }

        private Bitmap LoadNapasLogoBitmap()
        {
            var emb = DecodeBitmapFromBytes(GetEmbeddedResourceBytes("VietQR.Core.Resources.napas247.png"));
            if (emb != null) return emb;
            string path = GetDataFilePath("napas247.png");
            return File.Exists(path) ? DecodeBitmapFromBytes(File.ReadAllBytes(path)) : null;
        }

        private string GetDataFilePath(string fileName)
        {
            if (_assetResolver != null)
            {
                string resolved = _assetResolver.Resolve(fileName);
                if (!string.IsNullOrEmpty(resolved) && File.Exists(resolved)) return resolved;
            }

            if (!string.IsNullOrEmpty(_customRootPath))
            {
                string p = Path.Combine(_customRootPath, fileName);
                if (File.Exists(p)) return p;
                string pData = Path.Combine(Path.Combine(_customRootPath, "data"), fileName);
                if (File.Exists(pData)) return pData;
            }

            return Path.Combine(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources"), fileName);
        }

        private static string FormatAmountVnd(string rawAmount)
        {
            if (VietQrPayload.IsNullOrWhiteSpace(rawAmount)) return "0 VND";
            string cleaned = rawAmount.Replace(",", string.Empty).Replace(".", string.Empty).Trim();
            long val;
            if (long.TryParse(cleaned, NumberStyles.Integer, CultureInfo.InvariantCulture, out val))
            {
                var nfi = new NumberFormatInfo
                {
                    NumberGroupSeparator = ".",
                    NumberDecimalDigits = 0
                };
                return val.ToString("N0", nfi) + " VND";
            }
            return rawAmount + " VND";
        }

        private static Font CreatePreferredFont(float sizePx, FontStyle style)
        {
            EnsureEmbeddedFontLoaded();
            if (_embeddedFontFamily != null)
            {
                try
                {
                    if (_embeddedFontFamily.IsStyleAvailable(style))
                    {
                        return new Font(_embeddedFontFamily, sizePx, style, GraphicsUnit.Pixel);
                    }
                    if (_embeddedFontFamily.IsStyleAvailable(FontStyle.Regular))
                    {
                        return new Font(_embeddedFontFamily, sizePx, FontStyle.Regular, GraphicsUnit.Pixel);
                    }
                }
                catch
                {
                }
            }

            string[] fontNames = new string[] { "Segoe UI", "Arial", "Tahoma", "Microsoft Sans Serif" };
            for (int i = 0; i < fontNames.Length; i++)
            {
                try
                {
                    return new Font(fontNames[i], sizePx, style, GraphicsUnit.Pixel);
                }
                catch
                {
                }
            }

            return new Font(FontFamily.GenericSansSerif, sizePx, style, GraphicsUnit.Pixel);
        }

        private static void EnsureEmbeddedFontLoaded()
        {
            if (_fontInitialized) return;
            lock (FontLock)
            {
                if (_fontInitialized) return;
                _fontInitialized = true;

                try
                {
                    byte[] fontBytes = GetEmbeddedResourceBytes("VietQR.Core.Resources.fonts.GoogleSans-Regular.ttf");
                    if (fontBytes != null && fontBytes.Length > 0)
                    {
                        IntPtr fontPtr = Marshal.AllocCoTaskMem(fontBytes.Length);
                        try
                        {
                            Marshal.Copy(fontBytes, 0, fontPtr, fontBytes.Length);
                            _embeddedFonts = new PrivateFontCollection();
                            _embeddedFonts.AddMemoryFont(fontPtr, fontBytes.Length);
                            if (_embeddedFonts.Families.Length > 0)
                            {
                                _embeddedFontFamily = _embeddedFonts.Families[0];
                            }
                        }
                        finally
                        {
                            Marshal.FreeCoTaskMem(fontPtr);
                        }
                    }
                }
                catch
                {
                    _embeddedFontFamily = null;
                }
            }
        }
    }
}
