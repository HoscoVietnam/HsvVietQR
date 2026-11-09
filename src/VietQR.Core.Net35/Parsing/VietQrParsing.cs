using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace VietQR.Core
{
    /// <summary>
    /// Kết quả giải mã chuỗi mã QR chuẩn VietQR / EMVCo / NAPAS247.
    /// </summary>
    public sealed class VietQrInfo
    {
        public string BankBin { get; set; }
        public BankInfo BankInfo { get; set; }
        public string AccountNo { get; set; }
        public VietQrServiceType ServiceType { get; set; }
        public VietQrType QrType { get; set; }

        public bool IsDynamic
        {
            get { return QrType == VietQrType.Dynamic; }
        }

        public decimal? Amount { get; set; }
        public string Currency { get; set; }
        public string CountryCode { get; set; }
        public string Description { get; set; }
        public string BillNumber { get; set; }
        public string ReferenceLabel { get; set; }
        public string MerchantName { get; set; }
        public string MerchantCity { get; set; }
        public AdditionalDataOptions AdditionalData { get; set; }
        public string Crc { get; set; }
        public bool IsValidCrc { get; set; }
        public IDictionary<string, string> RawTags { get; set; }
        public string RawPayload { get; set; }

        public VietQrInfo()
        {
            BankBin = string.Empty;
            AccountNo = string.Empty;
            ServiceType = VietQrServiceType.Account;
            QrType = VietQrType.Static;
            RawTags = new Dictionary<string, string>(StringComparer.Ordinal);
            RawPayload = string.Empty;
        }
    }

    /// <summary>
    /// Cấu hình tùy chọn khi giải mã chuỗi VietQR.
    /// </summary>
    public sealed class VietQrParseOptions
    {
        public bool StrictCrc { get; set; }
        public int MaxPayloadLength { get; set; }
        public IBankCatalog BankCatalog { get; set; }

        public VietQrParseOptions()
        {
            StrictCrc = true;
            MaxPayloadLength = 1024;
        }
    }

    /// <summary>
    /// Bộ giải mã chuỗi văn bản mã QR chuẩn VietQR (EMVCo / NAPAS247).
    /// </summary>
    public static class VietQrParser
    {
        private const string VietQrGuid = "A000000727";

        public static Result<VietQrInfo> Parse(string qrString)
        {
            return Parse(qrString, null);
        }

        public static Result<VietQrInfo> Parse(string qrString, VietQrParseOptions options)
        {
            if (VietQrPayload.IsNullOrWhiteSpace(qrString))
            {
                return Result<VietQrInfo>.Fail(VietQrErrors.InvalidPayload);
            }

            var opt = options ?? new VietQrParseOptions();
            if (qrString.Length > opt.MaxPayloadLength)
            {
                return Result<VietQrInfo>.Fail(VietQrErrors.InvalidPayload);
            }

            bool isValidCrc = VietQr.VerifyCRC(qrString);
            if (opt.StrictCrc && !isValidCrc)
            {
                return Result<VietQrInfo>.Fail(VietQrErrors.InvalidCrc);
            }

            byte[] utf8Bytes = Encoding.UTF8.GetBytes(qrString);
            var rootTags = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!TryParseTlvBlocks(utf8Bytes, rootTags))
            {
                return Result<VietQrInfo>.Fail(VietQrErrors.InvalidPayload);
            }

            string tag38Content;
            if (!rootTags.TryGetValue("38", out tag38Content))
            {
                return Result<VietQrInfo>.Fail(VietQrErrors.NotVietQr);
            }

            var tag38SubTags = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!TryParseTlvBlocks(Encoding.UTF8.GetBytes(tag38Content), tag38SubTags))
            {
                return Result<VietQrInfo>.Fail(VietQrErrors.InvalidPayload);
            }

            string guid;
            if (!tag38SubTags.TryGetValue("00", out guid) ||
                !string.Equals(guid, VietQrGuid, StringComparison.OrdinalIgnoreCase))
            {
                return Result<VietQrInfo>.Fail(VietQrErrors.NotVietQr);
            }

            string benOrgContent;
            if (!tag38SubTags.TryGetValue("01", out benOrgContent))
            {
                return Result<VietQrInfo>.Fail(VietQrErrors.NotVietQr);
            }

            var benOrgSubTags = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!TryParseTlvBlocks(Encoding.UTF8.GetBytes(benOrgContent), benOrgSubTags))
            {
                return Result<VietQrInfo>.Fail(VietQrErrors.InvalidPayload);
            }

            string bankBin;
            if (!benOrgSubTags.TryGetValue("00", out bankBin) || VietQrPayload.IsNullOrWhiteSpace(bankBin))
            {
                return Result<VietQrInfo>.Fail(VietQrErrors.NotVietQr);
            }

            string accountNo;
            if (!benOrgSubTags.TryGetValue("01", out accountNo) || accountNo == null)
            {
                accountNo = string.Empty;
            }

            var serviceType = VietQrServiceType.Account;
            string serviceCode;
            if (tag38SubTags.TryGetValue("02", out serviceCode))
            {
                if (string.Equals(serviceCode, "QRIBFTTC", StringComparison.OrdinalIgnoreCase))
                {
                    serviceType = VietQrServiceType.Card;
                }
            }

            var info = new VietQrInfo
            {
                BankBin = bankBin.Trim(),
                AccountNo = accountNo.Trim(),
                ServiceType = serviceType,
                IsValidCrc = isValidCrc,
                RawPayload = qrString,
                RawTags = rootTags
            };

            var catalog = opt.BankCatalog ?? EmbeddedBankCatalog.Instance;
            info.BankInfo = catalog.Find(info.BankBin);

            string pointMethod;
            if (rootTags.TryGetValue("01", out pointMethod) && pointMethod != null)
            {
                info.QrType = string.Equals(pointMethod.Trim(), "12", StringComparison.Ordinal)
                    ? VietQrType.Dynamic
                    : VietQrType.Static;
            }

            string currency;
            if (rootTags.TryGetValue("53", out currency) && currency != null)
            {
                info.Currency = currency.Trim();
            }

            string amountStr;
            if (rootTags.TryGetValue("54", out amountStr) && amountStr != null)
            {
                decimal amt;
                if (decimal.TryParse(amountStr.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out amt))
                {
                    info.Amount = amt;
                }
            }

            string countryCode;
            if (rootTags.TryGetValue("58", out countryCode) && countryCode != null)
            {
                info.CountryCode = countryCode.Trim();
            }

            string merchantName;
            if (rootTags.TryGetValue("59", out merchantName) && merchantName != null)
            {
                info.MerchantName = merchantName.Trim();
            }

            string merchantCity;
            if (rootTags.TryGetValue("60", out merchantCity) && merchantCity != null)
            {
                info.MerchantCity = merchantCity.Trim();
            }

            string tag62Content;
            if (rootTags.TryGetValue("62", out tag62Content) && tag62Content != null)
            {
                var addTags = new Dictionary<string, string>(StringComparer.Ordinal);
                if (TryParseTlvBlocks(Encoding.UTF8.GetBytes(tag62Content), addTags))
                {
                    var addOptions = new AdditionalDataOptions();

                    string billNo;
                    if (addTags.TryGetValue("01", out billNo) && billNo != null)
                    {
                        addOptions.BillNumber = billNo.Trim();
                        info.BillNumber = addOptions.BillNumber;
                    }

                    string refLabel;
                    if (addTags.TryGetValue("05", out refLabel) && refLabel != null)
                    {
                        addOptions.ReferenceLabel = refLabel.Trim();
                        info.ReferenceLabel = addOptions.ReferenceLabel;
                    }

                    string purpose;
                    if (addTags.TryGetValue("08", out purpose) && purpose != null)
                    {
                        addOptions.Purpose = purpose.Trim();
                        info.Description = addOptions.Purpose;
                    }

                    info.AdditionalData = addOptions;
                }
            }

            string crcVal;
            if (rootTags.TryGetValue("63", out crcVal) && crcVal != null)
            {
                info.Crc = crcVal.Trim();
            }

            return Result<VietQrInfo>.Ok(info);
        }

        public static bool TryParse(string qrString, out VietQrInfo info)
        {
            var result = Parse(qrString);
            if (result.IsSuccess && result.Value != null)
            {
                info = result.Value;
                return true;
            }

            info = null;
            return false;
        }

        private static bool TryParseTlvBlocks(byte[] span, Dictionary<string, string> result)
        {
            int index = 0;
            while (index < span.Length)
            {
                if (span.Length - index < 4)
                {
                    return false;
                }

                byte t0 = span[index];
                byte t1 = span[index + 1];
                if (t0 < (byte)'0' || t0 > (byte)'9' || t1 < (byte)'0' || t1 > (byte)'9')
                {
                    return false;
                }

                byte l0 = span[index + 2];
                byte l1 = span[index + 3];
                if (l0 < (byte)'0' || l0 > (byte)'9' || l1 < (byte)'0' || l1 > (byte)'9')
                {
                    return false;
                }

                int length = (l0 - '0') * 10 + (l1 - '0');
                if (index + 4 + length > span.Length)
                {
                    return false;
                }

                string tag = new string(new char[] { (char)t0, (char)t1 });
                string value = Encoding.UTF8.GetString(span, index + 4, length);

                result[tag] = value;
                index += 4 + length;
            }

            return true;
        }
    }

    /// <summary>
    /// Bộ giải mã hình ảnh mã QR sang thông tin thanh toán VietQR trên .NET 3.5 (hỗ trợ System.Drawing.Bitmap, byte[], Stream, File).
    /// Tự động liên kết động với zxing.dll (ZXing.Net net35) nếu có trong thư mục ứng dụng mà không bắt buộc phụ thuộc cứng lúc biên dịch.
    /// </summary>
    public static class VietQrDecoder
    {
        public static string ReadRawQrText(Bitmap bitmap)
        {
            if (bitmap == null || bitmap.Width <= 0 || bitmap.Height <= 0)
            {
                return null;
            }

            try
            {
                Assembly zxingAsm = TryLoadZxingAssembly();
                if (zxingAsm == null) return null;

                Type readerType = zxingAsm.GetType("ZXing.BarcodeReader");
                if (readerType == null) return null;

                object reader = Activator.CreateInstance(readerType);
                PropertyInfo autoRotateProp = readerType.GetProperty("AutoRotate");
                if (autoRotateProp != null && autoRotateProp.CanWrite)
                {
                    autoRotateProp.SetValue(reader, true, null);
                }

                MethodInfo decodeMethod = readerType.GetMethod("Decode", new Type[] { typeof(Bitmap) });
                if (decodeMethod == null) return null;

                object decodeResult = decodeMethod.Invoke(reader, new object[] { bitmap });
                if (decodeResult == null) return null;

                PropertyInfo textProp = decodeResult.GetType().GetProperty("Text");
                return textProp != null ? textProp.GetValue(decodeResult, null) as string : decodeResult.ToString();
            }
            catch
            {
                return null;
            }
        }

        public static Result<VietQrInfo> Decode(Bitmap bitmap)
        {
            return Decode(bitmap, null);
        }

        public static Result<VietQrInfo> Decode(Bitmap bitmap, VietQrParseOptions options)
        {
            if (bitmap == null)
            {
                return Result<VietQrInfo>.Fail(VietQrErrors.ImageDecodeFailed);
            }

            string text = ReadRawQrText(bitmap);
            if (VietQrPayload.IsNullOrWhiteSpace(text))
            {
                return Result<VietQrInfo>.Fail(VietQrErrors.ImageDecodeFailed);
            }

            return VietQrParser.Parse(text, options);
        }

        public static Result<VietQrInfo> Decode(byte[] imageBytes)
        {
            return Decode(imageBytes, null);
        }

        public static Result<VietQrInfo> Decode(byte[] imageBytes, VietQrParseOptions options)
        {
            if (imageBytes == null || imageBytes.Length == 0)
            {
                return Result<VietQrInfo>.Fail(VietQrErrors.ImageDecodeFailed);
            }

            try
            {
                using (var ms = new MemoryStream(imageBytes))
                using (var bmp = new Bitmap(ms))
                {
                    return Decode(bmp, options);
                }
            }
            catch
            {
                return Result<VietQrInfo>.Fail(VietQrErrors.ImageDecodeFailed);
            }
        }

        public static Result<VietQrInfo> Decode(Stream imageStream)
        {
            return Decode(imageStream, null);
        }

        public static Result<VietQrInfo> Decode(Stream imageStream, VietQrParseOptions options)
        {
            if (imageStream == null)
            {
                return Result<VietQrInfo>.Fail(VietQrErrors.ImageDecodeFailed);
            }

            try
            {
                using (var bmp = new Bitmap(imageStream))
                {
                    return Decode(bmp, options);
                }
            }
            catch
            {
                return Result<VietQrInfo>.Fail(VietQrErrors.ImageDecodeFailed);
            }
        }

        public static Result<VietQrInfo> DecodeFile(string filePath)
        {
            return DecodeFile(filePath, null);
        }

        public static Result<VietQrInfo> DecodeFile(string filePath, VietQrParseOptions options)
        {
            if (VietQrPayload.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return Result<VietQrInfo>.Fail(VietQrErrors.ImageDecodeFailed);
            }

            try
            {
                byte[] bytes = File.ReadAllBytes(filePath);
                return Decode(bytes, options);
            }
            catch
            {
                return Result<VietQrInfo>.Fail(VietQrErrors.ImageDecodeFailed);
            }
        }

        private static Assembly TryLoadZxingAssembly()
        {
            Assembly[] loaded = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < loaded.Length; i++)
            {
                string name = loaded[i].GetName().Name;
                if (string.Equals(name, "zxing", StringComparison.OrdinalIgnoreCase))
                {
                    return loaded[i];
                }
            }

            try
            {
                string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "zxing.dll");
                if (File.Exists(localPath))
                {
                    return Assembly.LoadFrom(localPath);
                }
                return Assembly.Load("zxing");
            }
            catch
            {
                return null;
            }
        }
    }
}
