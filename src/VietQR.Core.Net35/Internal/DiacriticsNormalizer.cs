using System.Globalization;
using System.Text;

namespace VietQR.Core
{
    /// <summary>
    /// Tiện ích nội bộ chuẩn hóa tiếng Việt không dấu chuẩn ngân hàng và lọc ký tự điều khiển.
    /// </summary>
    internal static class DiacriticsNormalizer
    {
        public static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            string trimmed = text.Trim();
            if (trimmed.Length == 0) return string.Empty;

            bool isPureAscii = true;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] > 127 || char.IsControl(text[i]))
                {
                    isPureAscii = false;
                    break;
                }
            }
            if (isPureAscii) return text;

            string normalized = text.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);

            for (int i = 0; i < normalized.Length; i++)
            {
                char c = normalized[i];
                if (char.IsControl(c)) continue;
                if (c == 'đ') { sb.Append('d'); continue; }
                if (c == 'Đ') { sb.Append('D'); continue; }

                UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(c);
                if (category != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }

    /// <summary>
    /// Dữ liệu sinh mã VietQR thành công.
    /// </summary>
    public class VietQrGenerateResult
    {
        /// <summary>
        /// Chuỗi mã thanh toán VietQR theo chuẩn EMVCo (NAPAS247).
        /// </summary>
        public string QrCode { get; set; }

        /// <summary>
        /// Chuỗi Data URL ảnh Base64 (dùng trực tiếp cho thẻ img hoặc chuyển sang Bitmap).
        /// </summary>
        public string QrDataURL { get; set; }

        public VietQrGenerateResult()
        {
            QrCode = string.Empty;
            QrDataURL = string.Empty;
        }
    }
}

