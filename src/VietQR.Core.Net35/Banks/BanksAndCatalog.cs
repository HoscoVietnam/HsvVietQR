using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;

namespace VietQR.Core
{
    /// <summary>
    /// Giá trị mã BIN 6 chữ số của ngân hàng thành viên NAPAS.
    /// </summary>
    public sealed class BankBin : IEquatable<BankBin>
    {
        public string Value { get; private set; }

        private BankBin(string value)
        {
            Value = value;
        }

        public static BankBin Create(string raw)
        {
            string value = raw != null ? raw.Trim() : null;
            if (string.IsNullOrEmpty(value))
            {
                throw new ArgumentException("Mã ngân hàng (BIN hoặc mã viết tắt) không được để trống.", "raw");
            }

            if (value.Length == 6 && IsAllDigits(value))
            {
                return new BankBin(value);
            }

            // Tự động tra cứu trong danh mục ngân hàng nếu truyền mã viết tắt / tên ngân hàng (VD: "MB", "MBBank", "VCB", "BIDV")
            BankInfo found = EmbeddedBankCatalog.Instance.Find(value);
            if (found != null && !string.IsNullOrEmpty(found.Bin) && found.Bin.Length == 6 && IsAllDigits(found.Bin))
            {
                return new BankBin(found.Bin);
            }

            throw new ArgumentException(
                string.Format("Không nhận diện được mã ngân hàng '{0}'. Vui lòng truyền mã BIN 6 số (VD: 970422) hoặc tên viết tắt (VD: MB, VCB, BIDV).", raw),
                "raw");
        }

        public static bool TryCreate(string raw, out BankBin bin)
        {
            bin = null;
            string value = raw != null ? raw.Trim() : null;
            if (string.IsNullOrEmpty(value)) return false;

            if (value.Length == 6 && IsAllDigits(value))
            {
                bin = new BankBin(value);
                return true;
            }

            BankInfo found = EmbeddedBankCatalog.Instance.Find(value);
            if (found != null && !string.IsNullOrEmpty(found.Bin) && found.Bin.Length == 6 && IsAllDigits(found.Bin))
            {
                bin = new BankBin(found.Bin);
                return true;
            }

            return false;
        }

        private static bool IsAllDigits(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] < '0' || s[i] > '9') return false;
            }
            return true;
        }

        public override string ToString()
        {
            return Value;
        }

        public bool Equals(BankBin other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as BankBin);
        }

        public override int GetHashCode()
        {
            return Value != null ? Value.GetHashCode() : 0;
        }

        public static implicit operator string(BankBin bin)
        {
            return bin != null ? bin.Value : null;
        }

        public static implicit operator BankBin(string raw)
        {
            return Create(raw);
        }
    }

    /// <summary>
    /// Thông tin ngân hàng thành viên trong danh mục VietQR / NAPAS247.
    /// </summary>
    public sealed class BankInfo
    {
        /// <summary>Mã BIN 6 số (VD: "970422").</summary>
        public string Bin { get; private set; }

        /// <summary>Mã ngân hàng viết tắt (VD: "MB", "VCB").</summary>
        public string Code { get; private set; }

        /// <summary>Tên thường gọi (VD: "MBBank", "Vietcombank").</summary>
        public string ShortName { get; private set; }

        /// <summary>Tên đầy đủ của ngân hàng.</summary>
        public string Name { get; private set; }

        /// <summary>URL tham khảo của logo ngân hàng.</summary>
        public string Logo { get; private set; }

        /// <summary>
        /// Chuỗi hiển thị tiện lợi để gán trực tiếp vào ComboBox.DisplayMember trong WinForms
        /// (VD: "MBBank - Ngân hàng TMCP Quân Đội (970422)").
        /// </summary>
        public string DisplayName
        {
            get { return string.Format("{0} - {1} ({2})", ShortName, Name, Bin); }
        }

        public BankInfo(string bin, string code, string shortName, string name, string logo)
        {
            Bin = bin ?? string.Empty;
            Code = code ?? string.Empty;
            ShortName = shortName ?? string.Empty;
            Name = name ?? string.Empty;
            Logo = logo ?? string.Empty;
        }

        public override string ToString()
        {
            return DisplayName;
        }
    }

    /// <summary>
    /// Danh mục 76+ ngân hàng được nhúng trực tiếp trong DLL (chạy 100% offline, không phụ thuộc thư viện JSON ngoài).
    /// </summary>
    public sealed class EmbeddedBankCatalog : IBankCatalog
    {
        private static readonly object SyncLock = new object();
        private static EmbeddedBankCatalog _instance;

        public static EmbeddedBankCatalog Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (SyncLock)
                    {
                        if (_instance == null)
                        {
                            _instance = new EmbeddedBankCatalog();
                        }
                    }
                }
                return _instance;
            }
        }

        private readonly Dictionary<string, BankInfo> _byBin = new Dictionary<string, BankInfo>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, BankInfo> _byCode = new Dictionary<string, BankInfo>(StringComparer.OrdinalIgnoreCase);
        private readonly List<BankInfo> _allBanks = new List<BankInfo>();

        public EmbeddedBankCatalog()
        {
            LoadEmbeddedCatalog();
        }

        private void LoadEmbeddedCatalog()
        {
            try
            {
                var assembly = typeof(EmbeddedBankCatalog).Assembly;
                using (var stream = assembly.GetManifestResourceStream("VietQR.Core.Resources.banks.json"))
                {
                    if (stream == null) return;
                    using (var reader = new StreamReader(stream, Encoding.UTF8))
                    {
                        string json = reader.ReadToEnd();
                        var objects = SimpleBankJsonParser.ParseDataArray(json);
                        for (int i = 0; i < objects.Count; i++)
                        {
                            var item = objects[i];
                            string bin = GetField(item, "bin");
                            string code = GetField(item, "code");
                            string shortName = GetField(item, "shortName");
                            string name = GetField(item, "name");
                            string logo = GetField(item, "logo");

                            if (!string.IsNullOrEmpty(bin))
                            {
                                string effectiveShort = !string.IsNullOrEmpty(shortName) ? shortName : (!string.IsNullOrEmpty(code) ? code : bin);
                                var info = new BankInfo(bin, code, effectiveShort, name, logo);
                                _allBanks.Add(info);
                                _byBin[bin] = info;
                                if (!string.IsNullOrEmpty(code)) _byCode[code] = info;
                                if (!string.IsNullOrEmpty(shortName)) _byCode[shortName] = info;
                            }
                        }
                    }
                }
            }
            catch
            {
                // Bỏ qua lỗi nếu resource không đọc được
            }
        }

        private static string GetField(Dictionary<string, string> dict, string key)
        {
            string val;
            return dict.TryGetValue(key, out val) && val != null ? val : string.Empty;
        }

        public BankInfo Find(string binOrCode)
        {
            if (string.IsNullOrEmpty(binOrCode)) return null;
            string key = binOrCode.Trim();
            if (key.Length == 0) return null;

            BankInfo found;
            if (_byBin.TryGetValue(key, out found)) return found;
            if (_byCode.TryGetValue(key, out found)) return found;
            return null;
        }

        public IList<BankInfo> GetAll()
        {
            return new ReadOnlyCollection<BankInfo>(_allBanks);
        }

        /// <summary>
        /// Bộ phân tích JSON gọn nhẹ thuần C# dành riêng cho cấu trúc banks.json (Zero-dependency, hỗ trợ cả .NET 3.5 Client Profile).
        /// </summary>
        private static class SimpleBankJsonParser
        {
            public static List<Dictionary<string, string>> ParseDataArray(string json)
            {
                var list = new List<Dictionary<string, string>>();
                if (string.IsNullOrEmpty(json)) return list;

                int dataIdx = json.IndexOf("\"data\"", StringComparison.Ordinal);
                if (dataIdx < 0) return list;

                int arrStart = json.IndexOf('[', dataIdx);
                if (arrStart < 0) return list;

                int i = arrStart + 1;
                int len = json.Length;

                while (i < len)
                {
                    SkipWhitespace(json, ref i);
                    if (i >= len || json[i] == ']') break;

                    if (json[i] == '{')
                    {
                        var obj = ParseObject(json, ref i);
                        if (obj != null && obj.Count > 0)
                        {
                            list.Add(obj);
                        }
                    }
                    else
                    {
                        i++;
                    }
                }

                return list;
            }

            private static Dictionary<string, string> ParseObject(string json, ref int i)
            {
                var dict = new Dictionary<string, string>(StringComparer.Ordinal);
                int len = json.Length;
                i++; // skip '{'

                while (i < len)
                {
                    SkipWhitespace(json, ref i);
                    if (i >= len) break;
                    if (json[i] == '}')
                    {
                        i++;
                        break;
                    }
                    if (json[i] == ',')
                    {
                        i++;
                        continue;
                    }

                    if (json[i] != '"')
                    {
                        i++;
                        continue;
                    }

                    string key = ReadString(json, ref i);
                    SkipWhitespace(json, ref i);
                    if (i < len && json[i] == ':') i++;
                    SkipWhitespace(json, ref i);

                    string val = ReadValue(json, ref i);
                    if (!string.IsNullOrEmpty(key))
                    {
                        dict[key] = val;
                    }
                }

                return dict;
            }

            private static string ReadValue(string json, ref int i)
            {
                int len = json.Length;
                if (i >= len) return string.Empty;

                if (json[i] == '"')
                {
                    return ReadString(json, ref i);
                }

                int start = i;
                while (i < len && json[i] != ',' && json[i] != '}' && json[i] != ']' && !char.IsWhiteSpace(json[i]))
                {
                    i++;
                }
                string raw = json.Substring(start, i - start);
                if (string.Equals(raw, "null", StringComparison.OrdinalIgnoreCase)) return string.Empty;
                return raw;
            }

            private static string ReadString(string json, ref int i)
            {
                int len = json.Length;
                if (i >= len || json[i] != '"') return string.Empty;
                i++; // skip opening '"'

                var sb = new StringBuilder();
                while (i < len)
                {
                    char c = json[i++];
                    if (c == '"') break;
                    if (c == '\\' && i < len)
                    {
                        char esc = json[i++];
                        switch (esc)
                        {
                            case '"': sb.Append('"'); break;
                            case '\\': sb.Append('\\'); break;
                            case '/': sb.Append('/'); break;
                            case 'b': sb.Append('\b'); break;
                            case 'f': sb.Append('\f'); break;
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            case 'u':
                                if (i + 4 <= len)
                                {
                                    string hex = json.Substring(i, 4);
                                    ushort code;
                                    if (ushort.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code))
                                    {
                                        sb.Append((char)code);
                                    }
                                    i += 4;
                                }
                                break;
                            default:
                                sb.Append(esc);
                                break;
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }

                return sb.ToString();
            }

            private static void SkipWhitespace(string json, ref int i)
            {
                while (i < json.Length && char.IsWhiteSpace(json[i]))
                {
                    i++;
                }
            }
        }
    }
}

