using System;
using System.Drawing;
using System.Globalization;
using System.IO;

namespace VietQR.Core
{
    /// <summary>
    /// Tiện ích chuyển đổi và lưu ảnh mã VietQR (Base64, Bytes, Bitmap, File ảnh, HTML Image Tag).
    /// </summary>
    public static class VietQrImageConverter
    {
        public static byte[] ToBytes(string base64OrDataUrl)
        {
            if (VietQrPayload.IsNullOrWhiteSpace(base64OrDataUrl))
            {
                throw new ArgumentException("Chuỗi Base64 không được để trống.", "base64OrDataUrl");
            }

            string trimmed = base64OrDataUrl.Trim();
            int commaIndex = trimmed.IndexOf(',');
            string cleanBase64 = (commaIndex >= 0 && trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                ? trimmed.Substring(commaIndex + 1)
                : trimmed;

            return Convert.FromBase64String(cleanBase64);
        }

        public static MemoryStream ToStream(string base64OrDataUrl)
        {
            return new MemoryStream(ToBytes(base64OrDataUrl));
        }

        public static Bitmap ToBitmap(string base64OrDataUrl)
        {
            byte[] bytes = ToBytes(base64OrDataUrl);
            return ToBitmap(bytes);
        }

        /// <summary>
        /// Chuyển chuỗi Base64 hoặc Data URL (<c>data:image/png;base64,...</c>) thành đối tượng <see cref="Image"/> của WinForms.
        /// Tương thích trực tiếp 1-1 với hàm <c>Base64ToImage(string base64String)</c> trong WinForms truyền thống.
        /// </summary>
        public static Image Base64ToImage(string base64String)
        {
            return ToBitmap(base64String);
        }

        public static Bitmap ToBitmap(byte[] imageBytes)
        {
            if (imageBytes == null || imageBytes.Length == 0)
            {
                throw new ArgumentException("Mảng byte ảnh không được rỗng.", "imageBytes");
            }
            using (var ms = new MemoryStream(imageBytes))
            using (var temp = new Bitmap(ms))
            {
                return new Bitmap(temp);
            }
        }

        public static void SaveToFile(string base64OrDataUrl, string filePath)
        {
            if (VietQrPayload.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("Đường dẫn file (filePath) không được để trống.", "filePath");
            }

            byte[] bytes = ToBytes(base64OrDataUrl);
            SaveToFile(bytes, filePath);
        }

        public static void SaveToFile(byte[] imageBytes, string filePath)
        {
            if (imageBytes == null || imageBytes.Length == 0)
            {
                throw new ArgumentException("Mảng byte ảnh không được rỗng.", "imageBytes");
            }
            if (VietQrPayload.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("Đường dẫn file (filePath) không được để trống.", "filePath");
            }

            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllBytes(filePath, imageBytes);
        }

        public static string ToDataUrl(byte[] imageBytes)
        {
            if (imageBytes == null || imageBytes.Length == 0)
            {
                throw new ArgumentException("Mảng byte ảnh không được rỗng.", "imageBytes");
            }
            return "data:image/png;base64," + Convert.ToBase64String(imageBytes);
        }

        public static string ToHtmlImgTag(string base64OrDataUrl, string alt = "VietQR Payment", int? width = null, int? height = null)
        {
            string trimmed = base64OrDataUrl != null ? base64OrDataUrl.Trim() : string.Empty;
            string dataUrl = trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                ? trimmed
                : "data:image/png;base64," + trimmed;

            string styleAttr = string.Empty;
            if (width.HasValue || height.HasValue)
            {
                styleAttr = string.Format(
                    CultureInfo.InvariantCulture,
                    " style=\"{0}{1}\"",
                    width.HasValue ? string.Format(CultureInfo.InvariantCulture, "width:{0}px;", width.Value) : string.Empty,
                    height.HasValue ? string.Format(CultureInfo.InvariantCulture, "height:{0}px;", height.Value) : string.Empty);
            }

            return string.Format("<img src=\"{0}\" alt=\"{1}\"{2} />", dataUrl, alt, styleAttr);
        }
    }
}
