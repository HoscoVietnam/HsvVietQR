using System;
using System.Globalization;
using System.Text;

namespace VietQR.Core
{
    /// <summary>
    /// Trình vẽ mã VietQR dạng Vector SVG thuần C# (Zero native dependency).
    /// Đã tích hợp cơ chế escape XML chống XSS / XML Injection.
    /// </summary>
    public static class VietQrSvgRenderer
    {
        public static string Render(
            string emvQrContent,
            int size = 500,
            string darkColor = "#000000",
            string lightColor = "#FFFFFF",
            string centerLogoBase64 = null,
            float centerLogoRatio = 0.18f)
        {
            if (VietQrPayload.IsNullOrWhiteSpace(emvQrContent))
            {
                throw new ArgumentException("Chuỗi nội dung mã QR không được để trống.", "emvQrContent");
            }

            if (size <= 0) size = 500;
            if (VietQrPayload.IsNullOrWhiteSpace(darkColor)) darkColor = "#000000";
            if (VietQrPayload.IsNullOrWhiteSpace(lightColor)) lightColor = "#FFFFFF";

            string safeDark = XmlEscapeAttribute(darkColor);
            string safeLight = XmlEscapeAttribute(lightColor);

            bool[][] matrix = QrCodeMatrixGenerator.EncodeText(emvQrContent, QrEccLevel.Q);
            int activeModuleCount = matrix.Length;

            int padding = Math.Max(10, (int)(size * 0.025f));
            float innerSize = size - (padding * 2);
            float moduleSize = innerSize / activeModuleCount;

            int centerBoxStart = -1, centerBoxEnd = -1;
            float logoBoxSize = 0, logoBoxX = 0, logoBoxY = 0;
            bool hasCenterLogo = !VietQrPayload.IsNullOrWhiteSpace(centerLogoBase64);

            if (hasCenterLogo)
            {
                if (centerLogoRatio < 0.10f) centerLogoRatio = 0.10f;
                if (centerLogoRatio > 0.28f) centerLogoRatio = 0.28f;
                logoBoxSize = innerSize * centerLogoRatio;
                logoBoxX = padding + (innerSize - logoBoxSize) / 2f;
                logoBoxY = padding + (innerSize - logoBoxSize) / 2f;

                float relBoxStartX = (logoBoxX - padding) / moduleSize;
                float relBoxEndX = (logoBoxX + logoBoxSize - padding) / moduleSize;
                centerBoxStart = (int)Math.Floor(relBoxStartX);
                centerBoxEnd = (int)Math.Ceiling(relBoxEndX);
            }

            var sb = new StringBuilder(16384);
            sb.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "<svg xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\" viewBox=\"0 0 {0} {0}\" width=\"{0}\" height=\"{0}\">",
                size));

            sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                "  <rect width=\"100%\" height=\"100%\" fill=\"{0}\" />",
                safeLight));

            var pathBuilder = new StringBuilder();
            for (int row = 0; row < activeModuleCount; row++)
            {
                bool[] rowModules = matrix[row];
                for (int col = 0; col < activeModuleCount; col++)
                {
                    if (rowModules[col])
                    {
                        if (hasCenterLogo && row >= centerBoxStart && row <= centerBoxEnd && col >= centerBoxStart && col <= centerBoxEnd)
                        {
                            continue;
                        }

                        float x = padding + (col * moduleSize);
                        float y = padding + (row * moduleSize);
                        pathBuilder.Append(string.Format(CultureInfo.InvariantCulture, "M{0:F2},{1:F2}h{2:F2}v{2:F2}h-{2:F2}z ", x, y, moduleSize));
                    }
                }
            }

            if (pathBuilder.Length > 0)
            {
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "  <path fill=\"{0}\" d=\"{1}\" />",
                    safeDark,
                    pathBuilder.ToString().TrimEnd()));
            }

            if (hasCenterLogo)
            {
                float bgPadding = 4f;
                float bgX = logoBoxX - bgPadding;
                float bgY = logoBoxY - bgPadding;
                float bgSize = logoBoxSize + (bgPadding * 2);

                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "  <rect x=\"{0:F2}\" y=\"{1:F2}\" width=\"{2:F2}\" height=\"{2:F2}\" rx=\"6\" ry=\"6\" fill=\"{3}\" />",
                    bgX, bgY, bgSize, safeLight));

                string cleanHref = centerLogoBase64.Trim();
                if (!cleanHref.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    cleanHref = "data:image/png;base64," + cleanHref;
                }
                cleanHref = XmlEscapeAttribute(cleanHref);

                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "  <image href=\"{0}\" x=\"{1:F2}\" y=\"{2:F2}\" width=\"{3:F2}\" height=\"{3:F2}\" preserveAspectRatio=\"xMidYMid meet\" />",
                    cleanHref, logoBoxX, logoBoxY, logoBoxSize));
            }

            sb.AppendLine("</svg>");
            return sb.ToString();
        }

        private static string XmlEscapeAttribute(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value
                .Replace("&", "&amp;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }
    }
}
