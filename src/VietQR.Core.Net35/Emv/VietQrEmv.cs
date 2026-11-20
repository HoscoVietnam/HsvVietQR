using System;
using System.Globalization;
using System.Text;

namespace VietQR.Core
{
    /// <summary>
    /// Loại dịch vụ chuyển tiền NAPAS247 (theo mục 5.2.3.2.3 tài liệu NAPAS).
    /// </summary>
    public enum VietQrServiceType
    {
        /// <summary>
        /// QRIBFTTA: Dịch vụ chuyển nhanh NAPAS247 đến Tài khoản.
        /// </summary>
        Account = 0,

        /// <summary>
        /// QRIBFTTC: Dịch vụ chuyển nhanh NAPAS247 đến Thẻ.
        /// </summary>
        Card = 1
    }

    /// <summary>
    /// Loại mã VietQR theo chuẩn khởi tạo NAPAS247 / EMVCo (Tag 01 Point of Initiation Method).
    /// </summary>
    public enum VietQrType
    {
        /// <summary>
        /// Mã QR Tĩnh (Tag 01 = "11"): Dùng nhiều lần, không gắn cứng số tiền.
        /// </summary>
        Static = 0,

        /// <summary>
        /// Mã QR Động (Tag 01 = "12"): Dùng theo từng giao dịch, gắn sẵn số tiền thanh toán.
        /// </summary>
        Dynamic = 1
    }

    /// <summary>
    /// Các trường dữ liệu bổ sung trong Tag 62 (Additional Data Field Template) theo chuẩn EMVCo và NAPAS247.
    /// </summary>
    public sealed class AdditionalDataOptions
    {
        /// <summary>Sub-tag 01: Số hóa đơn / mã chứng từ (Bill Number - tối đa 25 ký tự).</summary>
        public string BillNumber { get; set; }

        /// <summary>Sub-tag 05: Mã đơn hàng / mã tham chiếu giao dịch (Reference Label - tối đa 25 ký tự).</summary>
        public string ReferenceLabel { get; set; }

        /// <summary>Sub-tag 08: Mục đích giao dịch / Nội dung chuyển khoản (Purpose of Transaction - tối đa 95 byte trong khung 99 byte của Tag 62).</summary>
        public string Purpose { get; set; }
    }

    /// <summary>
    /// Builder TLV thuần.
    /// </summary>
    public sealed class TlvBuilder
    {
        private static readonly string[] TwoDigitLookup = CreateTwoDigitLookup();
        private readonly StringBuilder _buffer = new StringBuilder();

        private static string[] CreateTwoDigitLookup()
        {
            var arr = new string[100];
            for (int i = 0; i < 100; i++)
            {
                arr[i] = i.ToString("D2", CultureInfo.InvariantCulture);
            }
            return arr;
        }

        public TlvBuilder Add(string tag, string value)
        {
            if (string.IsNullOrEmpty(value)) return this;
            int byteLength = Encoding.UTF8.GetByteCount(value);
            if (byteLength > 99)
            {
                throw new ArgumentOutOfRangeException("value", string.Format("TLV tag {0} vượt 99 byte UTF-8.", tag));
            }

            _buffer.Append(tag)
                   .Append(TwoDigitLookup[byteLength])
                   .Append(value);
            return this;
        }

        public string Build()
        {
            return _buffer.ToString();
        }

        public int Length
        {
            get { return _buffer.Length; }
        }

        public override string ToString()
        {
            return Build();
        }
    }

    /// <summary>
    /// Yêu cầu sinh mã thanh toán VietQR chuẩn NAPAS247.
    /// </summary>
    public sealed class VietQrRequest
    {
        public BankBin Bin { get; set; }
        public string AccountNo { get; set; }
        public long? Amount { get; set; }
        public string Description { get; set; }
        public VietQrServiceType ServiceType { get; set; }
        public string BillNumber { get; set; }
        public VietQrType? QrType { get; set; }

        public VietQrRequest(
            BankBin bin,
            string accountNo,
            long? amount = null,
            string description = null,
            VietQrServiceType serviceType = VietQrServiceType.Account,
            string billNumber = null,
            VietQrType? qrType = null)
        {
            Bin = bin;
            AccountNo = accountNo;
            Amount = amount;
            Description = description;
            ServiceType = serviceType;
            BillNumber = billNumber;
            QrType = qrType;
        }

        public VietQrType EffectiveQrType
        {
            get
            {
                if (QrType.HasValue) return QrType.Value;
                return (Amount.HasValue && Amount.Value > 0) ? VietQrType.Dynamic : VietQrType.Static;
            }
        }

        public static VietQrRequest CreateStatic(
            BankBin bin,
            string accountNo,
            VietQrServiceType serviceType = VietQrServiceType.Account,
            string description = null)
        {
            return new VietQrRequest(bin, accountNo, null, description, serviceType, null, VietQrType.Static);
        }

        public static VietQrRequest CreateDynamic(
            BankBin bin,
            string accountNo,
            long amount,
            VietQrServiceType serviceType = VietQrServiceType.Account,
            string description = null,
            string billNumber = null)
        {
            return new VietQrRequest(bin, accountNo, amount, description, serviceType, billNumber, VietQrType.Dynamic);
        }

        public static VietQrRequest CreateStaticAccount(BankBin bin, string accountNo, string description = null)
        {
            return CreateStatic(bin, accountNo, VietQrServiceType.Account, description);
        }

        public static VietQrRequest CreateStaticCard(BankBin bin, string cardNumber, string description = null)
        {
            return CreateStatic(bin, cardNumber, VietQrServiceType.Card, description);
        }

        public static VietQrRequest CreateDynamicAccount(BankBin bin, string accountNo, long amount, string description = null, string billNumber = null)
        {
            return CreateDynamic(bin, accountNo, amount, VietQrServiceType.Account, description, billNumber);
        }

        public static VietQrRequest CreateDynamicCard(BankBin bin, string cardNumber, long amount, string description = null, string billNumber = null)
        {
            return CreateDynamic(bin, cardNumber, amount, VietQrServiceType.Card, description, billNumber);
        }
    }

    /// <summary>
    /// Tiện ích đóng gói chuỗi payload EMVCo VietQR chuẩn NAPAS247.
    /// </summary>
    public static class VietQrPayload
    {
        public static string Create(
            BankBin bin,
            string accountNo,
            VietQrType qrType,
            VietQrServiceType serviceType = VietQrServiceType.Account,
            long? amount = null,
            string description = null,
            string billNumber = null)
        {
            if (bin == null) throw new ArgumentNullException("bin");
            if (qrType == VietQrType.Dynamic && (!amount.HasValue || amount.Value <= 0))
            {
                throw new ArgumentOutOfRangeException("amount", "Mã QR động (VietQrType.Dynamic) bắt buộc phải có số tiền lớn hơn 0.");
            }

            var builder = new VietQr()
                .SetBeneficiaryOrganization(bin.Value, accountNo, serviceType)
                .SetPointOfInitiationMethod(qrType == VietQrType.Dynamic);

            if (amount.HasValue && amount.Value > 0)
            {
                builder.SetTransactionAmount(amount.Value.ToString(CultureInfo.InvariantCulture));
            }

            if (!IsNullOrWhiteSpace(description) || !IsNullOrWhiteSpace(billNumber))
            {
                builder.SetAdditionalData(purpose: description, billNumber: billNumber);
            }

            return builder.Build();
        }

        public static string Create(BankBin bin, string accountNo, long? amount = null, string description = null)
        {
            return Create(bin, accountNo, amount, description, VietQrServiceType.Account, null);
        }

        public static string Create(
            BankBin bin,
            string accountNo,
            long? amount,
            string description,
            VietQrServiceType serviceType,
            string billNumber = null)
        {
            var qrType = (amount.HasValue && amount.Value > 0) ? VietQrType.Dynamic : VietQrType.Static;
            return Create(bin, accountNo, qrType, serviceType, amount, description, billNumber);
        }

        public static string CreateStatic(BankBin bin, string accountNo, string description = null)
        {
            return Create(bin, accountNo, VietQrType.Static, VietQrServiceType.Account, null, description, null);
        }

        public static string CreateStatic(
            BankBin bin,
            string accountNo,
            VietQrServiceType serviceType,
            string description = null)
        {
            return Create(bin, accountNo, VietQrType.Static, serviceType, null, description, null);
        }

        public static string CreateDynamic(BankBin bin, string accountNo, long amount, string description = null, string billNumber = null)
        {
            return Create(bin, accountNo, VietQrType.Dynamic, VietQrServiceType.Account, amount, description, billNumber);
        }

        public static string CreateDynamic(
            BankBin bin,
            string accountNo,
            long amount,
            VietQrServiceType serviceType,
            string description = null,
            string billNumber = null)
        {
            return Create(bin, accountNo, VietQrType.Dynamic, serviceType, amount, description, billNumber);
        }

        public static string CreateStaticAccount(BankBin bin, string accountNo, string description = null)
        {
            return CreateStatic(bin, accountNo, VietQrServiceType.Account, description);
        }

        public static string CreateStaticCard(BankBin bin, string cardNumber, string description = null)
        {
            return CreateStatic(bin, cardNumber, VietQrServiceType.Card, description);
        }

        public static string CreateDynamicAccount(BankBin bin, string accountNo, long amount, string description = null, string billNumber = null)
        {
            return CreateDynamic(bin, accountNo, amount, VietQrServiceType.Account, description, billNumber);
        }

        public static string CreateDynamicCard(BankBin bin, string cardNumber, long amount, string description = null, string billNumber = null)
        {
            return CreateDynamic(bin, cardNumber, amount, VietQrServiceType.Card, description, billNumber);
        }

        internal static bool IsNullOrWhiteSpace(string value)
        {
            if (value == null) return true;
            for (int i = 0; i < value.Length; i++)
            {
                if (!char.IsWhiteSpace(value[i])) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// Core tạo chuỗi mã thanh toán VietQR (chuẩn EMVCo / NAPAS247).
    /// </summary>
    public class VietQr : IQrGenerator
    {
        public string Generate(VietQrRequest request)
        {
            if (request == null) throw new ArgumentNullException("request");
            return VietQrPayload.Create(
                request.Bin,
                request.AccountNo,
                request.EffectiveQrType,
                request.ServiceType,
                request.Amount,
                request.Description,
                request.BillNumber);
        }

        public const string DefaultPayloadFormatIndicator = "000201";
        public const string MethodStatic = "010211";
        public const string MethodDynamic = "010212";
        public const string NapasAid = "0010A000000727";
        public const string ServiceCodeAccount = "0208QRIBFTTA";
        public const string ServiceCodeCard = "0208QRIBFTTC";
        public const string DefaultCurrency = "5303704";
        public const string DefaultCountry = "5802VN";

        private static readonly string[] TwoDigitLookup = CreateTwoDigitLookup();

        private string _pointOfInitiationMethod = MethodStatic;
        private string _consumerAccountInformation = string.Empty;
        private string _transactionAmount = string.Empty;
        private string _merchantName = string.Empty;
        private string _merchantCity = string.Empty;
        private string _additionalDataFieldTemplate = string.Empty;
        private bool _manualMethodOverride = false;

        private static string[] CreateTwoDigitLookup()
        {
            var arr = new string[100];
            for (int i = 0; i < 100; i++)
            {
                arr[i] = i.ToString("D2", CultureInfo.InvariantCulture);
            }
            return arr;
        }

        public static string ConvertLength(int length)
        {
            if ((uint)length < 100u) return TwoDigitLookup[length];
            throw new ArgumentOutOfRangeException("length", string.Format("Độ dài trường TLV ({0}) vượt quá giới hạn tối đa 99 byte của chuẩn EMVCo/NAPAS.", length));
        }

        private static string ConvertByteLength(string value)
        {
            return ConvertLength(Encoding.UTF8.GetByteCount(value));
        }

        public static string RemoveDiacritics(string text)
        {
            return DiacriticsNormalizer.Normalize(text);
        }

        public VietQr SetPointOfInitiationMethod(bool isDynamic)
        {
            _pointOfInitiationMethod = isDynamic ? MethodDynamic : MethodStatic;
            _manualMethodOverride = true;
            return this;
        }

        public VietQr SetPointOfInitiationMethod(VietQrType qrType)
        {
            return SetPointOfInitiationMethod(qrType == VietQrType.Dynamic);
        }

        public VietQr SetTransactionAmount(string money)
        {
            if (VietQrPayload.IsNullOrWhiteSpace(money))
            {
                _transactionAmount = string.Empty;
                if (!_manualMethodOverride) _pointOfInitiationMethod = MethodStatic;
                return this;
            }

            string trimmed = money.Trim();
            ulong val;
            if (!IsAllDigits(trimmed) || trimmed.Length > 13 || !ulong.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out val) || val == 0)
            {
                throw new ArgumentException("Invalid amount - Trường chỉ nhập số, tối đa 13 kí tự", "money");
            }

            string clean = val.ToString(CultureInfo.InvariantCulture);
            string length = ConvertByteLength(clean);
            _transactionAmount = string.Concat("54", length, clean);

            if (!_manualMethodOverride) _pointOfInitiationMethod = MethodDynamic;
            return this;
        }

        public VietQr SetBeneficiaryOrganization(
            string acquirerId,
            string consumerId,
            VietQrServiceType serviceType = VietQrServiceType.Account)
        {
            string acquirerTrimmed = acquirerId != null ? acquirerId.Trim() : string.Empty;
            string consumerTrimmed = consumerId != null ? consumerId.Trim() : string.Empty;

            if (acquirerTrimmed.Length != 6 || !IsAllDigits(acquirerTrimmed))
            {
                BankInfo resolvedBank = EmbeddedBankCatalog.Instance.Find(acquirerTrimmed);
                if (resolvedBank != null && !string.IsNullOrEmpty(resolvedBank.Bin) && resolvedBank.Bin.Length == 6 && IsAllDigits(resolvedBank.Bin))
                {
                    acquirerTrimmed = resolvedBank.Bin;
                }
                else
                {
                    throw new ArgumentException("Mã ngân hàng (acquirerId / bankBin) bắt buộc phải là đúng 6 chữ số hoặc mã ngân hàng hợp lệ (VD: 970422, MB, VCB).", "acquirerId");
                }
            }

            if (string.IsNullOrEmpty(consumerTrimmed) || Encoding.UTF8.GetByteCount(consumerTrimmed) > 25)
            {
                throw new ArgumentException(
                    string.Format("Số tài khoản/thẻ (consumerId / accountNo) không được rỗng và tối đa 25 ký tự theo chuẩn NAPAS / Virtual Account (nhận được: {0} ký tự).", consumerTrimmed.Length),
                    "consumerId");
            }

            string sub00 = string.Concat("00", ConvertByteLength(acquirerTrimmed), acquirerTrimmed);
            string sub01 = string.Concat("01", ConvertByteLength(consumerTrimmed), consumerTrimmed);
            string sub01Total = string.Concat(sub00, sub01);

            string beneficiaryOrg = string.Concat("01", ConvertByteLength(sub01Total), sub01Total);
            string serviceCode = serviceType == VietQrServiceType.Card ? ServiceCodeCard : ServiceCodeAccount;

            string tag38Value = string.Concat(NapasAid, beneficiaryOrg, serviceCode);
            _consumerAccountInformation = string.Concat("38", ConvertByteLength(tag38Value), tag38Value);
            return this;
        }

        public VietQr SetAdditionalData(AdditionalDataOptions options)
        {
            if (options == null)
            {
                _additionalDataFieldTemplate = string.Empty;
                return this;
            }

            return SetAdditionalData(options.Purpose, options.BillNumber, options.ReferenceLabel);
        }

        public VietQr SetAdditionalData(
            string purpose = null,
            string billNumber = null,
            string referenceLabel = null)
        {
            var innerSb = new StringBuilder();

            if (!VietQrPayload.IsNullOrWhiteSpace(billNumber))
            {
                AppendSubTag(innerSb, "01", billNumber, 25);
                if (!_manualMethodOverride) _pointOfInitiationMethod = MethodDynamic;
            }

            if (!VietQrPayload.IsNullOrWhiteSpace(referenceLabel))
            {
                AppendSubTag(innerSb, "05", referenceLabel, 25);
                if (!_manualMethodOverride) _pointOfInitiationMethod = MethodDynamic;
            }

            // Sub-tag 08: Purpose of Transaction (Nội dung CK - hỗ trợ tới 95 byte trong khung 99 byte của Tag 62)
            AppendSubTag(innerSb, "08", purpose, 95);

            string inner = innerSb.ToString();
            _additionalDataFieldTemplate = inner.Length > 0
                ? string.Concat("62", ConvertByteLength(inner), inner)
                : string.Empty;
            return this;
        }

        private static void AppendSubTag(StringBuilder innerSb, string id, string val, int maxLen)
        {
            if (VietQrPayload.IsNullOrWhiteSpace(val)) return;
            string clean = RemoveDiacritics(val.Trim());
            int currentBytes = Encoding.UTF8.GetByteCount(innerSb.ToString());
            int remainingSpace = 99 - currentBytes - 4;
            if (remainingSpace <= 0) return;
            int limit = Math.Min(maxLen, remainingSpace);
            if (clean.Length > limit) clean = clean.Substring(0, limit);
            while (Encoding.UTF8.GetByteCount(clean) > limit && clean.Length > 0)
            {
                clean = clean.Substring(0, clean.Length - 1);
            }
            if (clean.Length == 0) return;
            innerSb.Append(id).Append(ConvertByteLength(clean)).Append(clean);
        }

        public VietQr SetMerchantInfo(string name, string city = "HANOI")
        {
            if (!VietQrPayload.IsNullOrWhiteSpace(name))
            {
                string n = RemoveDiacritics(name.Trim()).ToUpperInvariant();
                if (n.Length > 25) n = n.Substring(0, 25);
                _merchantName = string.Concat("59", ConvertByteLength(n), n);
            }
            else
            {
                _merchantName = string.Empty;
            }

            if (!VietQrPayload.IsNullOrWhiteSpace(city))
            {
                string c = RemoveDiacritics(city.Trim()).ToUpperInvariant();
                if (c.Length > 15) c = c.Substring(0, 15);
                _merchantCity = string.Concat("60", ConvertByteLength(c), c);
            }
            else
            {
                _merchantCity = string.Empty;
            }

            return this;
        }

        public VietQr SetAdditionalDataFieldTemplate(string content)
        {
            return SetAdditionalData(purpose: content);
        }

        private static readonly ushort[] s_crcTable = new ushort[]
        {
            0x0000, 0x1021, 0x2042, 0x3063, 0x4084, 0x50a5, 0x60c6, 0x70e7,
            0x8108, 0x9129, 0xa14a, 0xb16b, 0xc18c, 0xd1ad, 0xe1ce, 0xf1ef,
            0x1231, 0x0210, 0x3273, 0x2252, 0x52b5, 0x4294, 0x72f7, 0x62d6,
            0x9339, 0x8318, 0xb37b, 0xa35a, 0xd3bd, 0xc39c, 0xf3ff, 0xe3de,
            0x2462, 0x3443, 0x0420, 0x1401, 0x64e6, 0x74c7, 0x44a4, 0x5485,
            0xa56a, 0xb54b, 0x8528, 0x9509, 0xe5ee, 0xf5cf, 0xc5ac, 0xd58d,
            0x3653, 0x2672, 0x1611, 0x0630, 0x76d7, 0x66f6, 0x5695, 0x46b4,
            0xb75b, 0xa77a, 0x9719, 0x8738, 0xf7df, 0xe7fe, 0xd79d, 0xc7bc,
            0x48c4, 0x58e5, 0x6886, 0x78a7, 0x0840, 0x1861, 0x2802, 0x3823,
            0xc9cc, 0xd9ed, 0xe98e, 0xf9af, 0x8948, 0x9969, 0xa90a, 0xb92b,
            0x5af5, 0x4ad4, 0x7ab7, 0x6a96, 0x1a71, 0x0a50, 0x3a33, 0x2a12,
            0xdbfd, 0xcbdc, 0xfbbf, 0xeb9e, 0x9b79, 0x8b58, 0xbb3b, 0xab1a,
            0x6ca6, 0x7c87, 0x4ce4, 0x5cc5, 0x2c22, 0x3c03, 0x0c60, 0x1c41,
            0xedae, 0xfd8f, 0xcdec, 0xddcd, 0xad2a, 0xbd0b, 0x8d68, 0x9d49,
            0x7e97, 0x6eb6, 0x5ed5, 0x4ef4, 0x3e13, 0x2e32, 0x1e51, 0x0e70,
            0xff9f, 0xefbe, 0xdfdd, 0xcffc, 0xbf1b, 0xaf3a, 0x9f59, 0x8f78,
            0x9188, 0x81a9, 0xb1ca, 0xa1eb, 0xd10c, 0xc12d, 0xf14e, 0xe16f,
            0x1080, 0x00a1, 0x30c2, 0x20e3, 0x5004, 0x4025, 0x7046, 0x6067,
            0x83b9, 0x9398, 0xa3fb, 0xb3da, 0xc33d, 0xd31c, 0xe37f, 0xf35e,
            0x02b1, 0x1290, 0x22f3, 0x32d2, 0x4235, 0x5214, 0x6277, 0x7256,
            0xb5ea, 0xa5cb, 0x95a8, 0x8589, 0xf56e, 0xe54f, 0xd52c, 0xc50d,
            0x34e2, 0x24c3, 0x14a0, 0x0481, 0x7466, 0x6447, 0x5424, 0x4405,
            0xa7db, 0xb7fa, 0x8799, 0x97b8, 0xe75f, 0xf77e, 0xc71d, 0xd73c,
            0x26d3, 0x36f2, 0x0691, 0x16b0, 0x6657, 0x7676, 0x4615, 0x5634,
            0xd94c, 0xc96d, 0xf90e, 0xe92f, 0x99c8, 0x89e9, 0xb98a, 0xa9ab,
            0x5844, 0x4865, 0x7806, 0x6827, 0x18c0, 0x08e1, 0x3882, 0x28a3,
            0xcb7d, 0xdb5c, 0xeb3f, 0xfb1e, 0x8bf9, 0x9bd8, 0xabbb, 0xbb9a,
            0x4a75, 0x5a54, 0x6a37, 0x7a16, 0x0af1, 0x1ad0, 0x2ab3, 0x3a92,
            0xfd2e, 0xed0f, 0xdd6c, 0xcd4d, 0xbdaa, 0xad8b, 0x9de8, 0x8dc9,
            0x7c26, 0x6c07, 0x5c64, 0x4c45, 0x3ca2, 0x2c83, 0x1ce0, 0x0cc1,
            0xef1f, 0xff3e, 0xcf5d, 0xdf7c, 0xaf9b, 0xbfba, 0x8fd9, 0x9ff8,
            0x6e17, 0x7e36, 0x4e55, 0x5e74, 0x2e93, 0x3eb2, 0x0ed1, 0x1ef0
        };

        public static int CalcCRC(string str)
        {
            if (string.IsNullOrEmpty(str)) return 0xFFFF;
            return CalcCRC(str, 0, str.Length);
        }

        private static int CalcCRC(string str, int offset, int count)
        {
            ushort crc = 0xFFFF;
            int end = offset + count;
            for (int i = offset; i < end; i++)
            {
                int c = str[i];
                crc = (ushort)(s_crcTable[((crc >> 8) ^ c) & 0xFF] ^ (crc << 8));
            }
            return crc;
        }

        public static string GenCRCCode(string content)
        {
            if (content == null) return "FFFF";
            return CalcCRC(content, 0, content.Length).ToString("X4", CultureInfo.InvariantCulture);
        }

        public static bool VerifyCRC(string content)
        {
            if (string.IsNullOrEmpty(content) || content.Length < 8) return false;

            int len = content.Length;
            if (content[len - 8] != '6' ||
                content[len - 7] != '3' ||
                content[len - 6] != '0' ||
                content[len - 5] != '4')
            {
                return false;
            }

            int expectedCrc = CalcCRC(content, 0, len - 4);
            string crcHex = content.Substring(len - 4, 4);
            ushort parsedCrc;
            if (ushort.TryParse(crcHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out parsedCrc))
            {
                return parsedCrc == expectedCrc;
            }

            return false;
        }

        public static string ExtractBinFromEmvCo(string content)
        {
            if (VietQrPayload.IsNullOrWhiteSpace(content)) return null;

            var parseResult = VietQrParser.Parse(content, new VietQrParseOptions { StrictCrc = false });
            if (parseResult.IsSuccess && parseResult.Value != null && !string.IsNullOrEmpty(parseResult.Value.BankBin))
            {
                return parseResult.Value.BankBin;
            }

            int aidIdx = content.IndexOf("A000000727", StringComparison.OrdinalIgnoreCase);
            if (aidIdx >= 0)
            {
                int sub00Idx = content.IndexOf("0006", aidIdx + 10, StringComparison.Ordinal);
                if (sub00Idx >= 0 && sub00Idx + 10 <= content.Length)
                {
                    string candidate = content.Substring(sub00Idx + 4, 6);
                    if (IsAllDigits(candidate)) return candidate;
                }
            }

            return null;
        }

        public string Build()
        {
            var sb = new StringBuilder(256);
            sb.Append(DefaultPayloadFormatIndicator)
              .Append(_pointOfInitiationMethod)
              .Append(_consumerAccountInformation)
              .Append(DefaultCurrency)
              .Append(_transactionAmount)
              .Append(DefaultCountry)
              .Append(_merchantName)
              .Append(_merchantCity)
              .Append(_additionalDataFieldTemplate)
              .Append("6304");

            string contentQR = sb.ToString();
            string crcValue = CalcCRC(contentQR, 0, contentQR.Length).ToString("X4", CultureInfo.InvariantCulture);
            return string.Concat(contentQR, crcValue);
        }

        private static bool IsAllDigits(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] < '0' || s[i] > '9') return false;
            }
            return true;
        }
    }
}
