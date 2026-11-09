using System;
using System.Collections.Generic;
using System.Text;

namespace VietQR.Core
{
    /// <summary>
    /// Mức độ sửa lỗi Reed-Solomon của mã QR theo chuẩn ISO/IEC 18004.
    /// </summary>
    public enum QrEccLevel
    {
        /// <summary>Khôi phục ~7% dữ liệu.</summary>
        L = 0,
        /// <summary>Khôi phục ~15% dữ liệu.</summary>
        M = 1,
        /// <summary>Khôi phục ~25% dữ liệu (Mặc định chuẩn VietQR).</summary>
        Q = 2,
        /// <summary>Khôi phục ~30% dữ liệu.</summary>
        H = 3
    }

    /// <summary>
    /// Trình tạo ma trận điểm ảnh QR Code (ISO/IEC 18004) thuần C# dành cho .NET Framework 3.5 (Zero-Dependency).
    /// Hỗ trợ đầy đủ Version 1..40, ECC Level L/M/Q/H, mã hóa Byte UTF-8, Reed-Solomon GF(256) và tự động chọn Mask tối ưu (0..7).
    /// </summary>
    internal static class QrCodeMatrixGenerator
    {
        // Bảng số lượng khối Error Correction cho mỗi (EccLevel, Version 1..40)
        private static readonly sbyte[][] NUM_ERROR_CORRECTION_BLOCKS = new sbyte[][]
        {
            // L
            new sbyte[] { -1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 4,  4,  4,  4,  4,  6,  6,  6,  6,  7,  8,  8,  9,  9, 10, 12, 12, 12, 13, 14, 15, 16, 17, 18, 19, 19, 20, 21, 22, 24, 25 },
            // M
            new sbyte[] { -1, 1, 1, 1, 2, 2, 4, 4, 4, 5, 5,  5,  8,  9,  9, 10, 10, 11, 13, 14, 16, 17, 17, 18, 20, 21, 23, 25, 26, 28, 29, 31, 33, 35, 37, 38, 40, 43, 45, 47, 49 },
            // Q
            new sbyte[] { -1, 1, 1, 2, 2, 4, 4, 6, 6, 8, 8,  8, 10, 12, 16, 12, 17, 16, 18, 21, 20, 23, 23, 25, 27, 29, 34, 34, 35, 38, 40, 43, 45, 48, 51, 53, 56, 59, 62, 65, 68 },
            // H
            new sbyte[] { -1, 1, 1, 2, 4, 4, 4, 5, 6, 8, 8, 11, 11, 16, 16, 18, 16, 19, 21, 25, 25, 25, 34, 30, 32, 35, 37, 40, 42, 45, 48, 51, 54, 57, 60, 63, 66, 70, 74, 77, 81 }
        };

        // Bảng số codeword ECC trên mỗi khối cho mỗi (EccLevel, Version 1..40)
        private static readonly sbyte[][] ECC_CODEWORDS_PER_BLOCK = new sbyte[][]
        {
            // L
            new sbyte[] { -1,  7, 10, 15, 20, 26, 18, 20, 24, 30, 18, 20, 24, 26, 30, 22, 24, 28, 30, 28, 28, 28, 28, 30, 30, 26, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30 },
            // M
            new sbyte[] { -1, 10, 16, 26, 18, 24, 16, 18, 22, 22, 26, 30, 22, 22, 24, 24, 28, 28, 26, 26, 26, 26, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28 },
            // Q
            new sbyte[] { -1, 13, 22, 18, 26, 18, 24, 18, 22, 20, 24, 28, 26, 24, 20, 30, 24, 28, 28, 26, 30, 28, 30, 30, 30, 30, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30 },
            // H
            new sbyte[] { -1, 17, 28, 22, 16, 22, 28, 26, 26, 24, 28, 24, 28, 22, 24, 24, 30, 28, 28, 26, 28, 30, 24, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30 }
        };

        /// <summary>
        /// Tạo ma trận điểm ảnh QR Code (true = điểm đen, false = điểm trắng) không bao gồm lề trắng (quiet zone).
        /// </summary>
        public static bool[][] EncodeText(string text, QrEccLevel eccLevel = QrEccLevel.Q)
        {
            if (text == null) throw new ArgumentNullException("text");
            byte[] utf8 = Encoding.UTF8.GetBytes(text);
            return EncodeBytes(utf8, eccLevel);
        }

        public static bool[][] EncodeBytes(byte[] data, QrEccLevel eccLevel = QrEccLevel.Q)
        {
            if (data == null) throw new ArgumentNullException("data");

            int eccOrd = (int)eccLevel;
            int version = 1;
            int dataCapacityBits = 0;

            for (version = 1; version <= 40; version++)
            {
                dataCapacityBits = GetNumDataCodewords(version, eccOrd) * 8;
                int charCountBits = version <= 9 ? 8 : 16;
                int neededBits = 4 + charCountBits + data.Length * 8;
                if (neededBits <= dataCapacityBits)
                {
                    break;
                }
            }

            if (version > 40)
            {
                throw new ArgumentException("Dữ liệu quá lớn so với dung lượng tối đa của mã QR (Version 40).");
            }

            var bitBuffer = new List<bool>(dataCapacityBits);
            // Mode indicator cho Byte Mode = 0100 (4 bits)
            AppendBits(0x4, 4, bitBuffer);
            // Character count indicator (8 bits cho V1..9, 16 bits cho V10..40)
            AppendBits(data.Length, version <= 9 ? 8 : 16, bitBuffer);
            // Payload bytes
            for (int i = 0; i < data.Length; i++)
            {
                AppendBits(data[i], 8, bitBuffer);
            }

            // Terminator (tối đa 4 bit 0)
            int terminatorLen = Math.Min(4, dataCapacityBits - bitBuffer.Count);
            AppendBits(0, terminatorLen, bitBuffer);

            // Pad to byte boundary
            int remBits = (8 - (bitBuffer.Count % 8)) % 8;
            AppendBits(0, remBits, bitBuffer);

            // Pad bytes luân phiên 0xEC và 0x11
            byte padByte = 0xEC;
            while (bitBuffer.Count < dataCapacityBits)
            {
                AppendBits(padByte, 8, bitBuffer);
                padByte ^= (byte)(0xEC ^ 0x11);
            }

            byte[] dataCodewords = new byte[bitBuffer.Count / 8];
            for (int i = 0; i < bitBuffer.Count; i++)
            {
                if (bitBuffer[i])
                {
                    dataCodewords[i >> 3] |= (byte)(1 << (7 - (i & 7)));
                }
            }

            byte[] allCodewords = AddEccAndInterleave(dataCodewords, version, eccOrd);
            int size = version * 4 + 17;
            bool[][] modules = CreateBoolMatrix(size);
            bool[][] isFunction = CreateBoolMatrix(size);

            DrawFunctionPatterns(version, eccOrd, modules, isFunction);
            DrawCodewords(allCodewords, version, modules, isFunction);

            // Chọn mask (0..7) có điểm phạt thấp nhất
            int bestMask = 0;
            long minPenalty = long.MaxValue;
            for (int mask = 0; mask < 8; mask++)
            {
                ApplyMask(mask, modules, isFunction);
                DrawFormatBits(eccOrd, mask, modules, isFunction);
                long penalty = GetPenaltyScore(modules);
                if (penalty < minPenalty)
                {
                    minPenalty = penalty;
                    bestMask = mask;
                }
                ApplyMask(mask, modules, isFunction); // hoàn tác XOR mask
            }

            ApplyMask(bestMask, modules, isFunction);
            DrawFormatBits(eccOrd, bestMask, modules, isFunction);

            return modules;
        }

        private static bool[][] CreateBoolMatrix(int size)
        {
            var m = new bool[size][];
            for (int i = 0; i < size; i++)
            {
                m[i] = new bool[size];
            }
            return m;
        }

        private static void AppendBits(int val, int numBits, List<bool> buffer)
        {
            for (int i = numBits - 1; i >= 0; i--)
            {
                buffer.Add(((val >> i) & 1) != 0);
            }
        }

        private static int GetNumRawDataModules(int ver)
        {
            int result = (16 * ver + 128) * ver + 64;
            if (ver >= 2)
            {
                int numAlign = ver / 7 + 2;
                result -= (25 * numAlign - 10) * numAlign - 55;
                if (ver >= 7)
                {
                    result -= 36;
                }
            }
            return result;
        }

        private static int GetNumDataCodewords(int ver, int eccOrd)
        {
            return GetNumRawDataModules(ver) / 8 -
                   ECC_CODEWORDS_PER_BLOCK[eccOrd][ver] * NUM_ERROR_CORRECTION_BLOCKS[eccOrd][ver];
        }

        private static byte[] AddEccAndInterleave(byte[] data, int ver, int eccOrd)
        {
            int numBlocks = NUM_ERROR_CORRECTION_BLOCKS[eccOrd][ver];
            int blockEccLen = ECC_CODEWORDS_PER_BLOCK[eccOrd][ver];
            int rawCodewords = GetNumRawDataModules(ver) / 8;
            int numShortBlocks = numBlocks - rawCodewords % numBlocks;
            int shortBlockLen = rawCodewords / numBlocks;

            byte[][] blocks = new byte[numBlocks][];
            byte[] rsDiv = ReedSolomonComputeDivisor(blockEccLen);

            int k = 0;
            for (int i = 0; i < numBlocks; i++)
            {
                int datLen = shortBlockLen - blockEccLen + (i < numShortBlocks ? 0 : 1);
                byte[] dat = new byte[datLen];
                Array.Copy(data, k, dat, 0, datLen);
                k += datLen;

                byte[] ecc = ReedSolomonComputeRemainder(dat, rsDiv);
                byte[] block = new byte[datLen + blockEccLen];
                Array.Copy(dat, 0, block, 0, datLen);
                Array.Copy(ecc, 0, block, datLen, blockEccLen);
                blocks[i] = block;
            }

            byte[] result = new byte[rawCodewords];
            int pos = 0;
            int maxDataLen = shortBlockLen - blockEccLen + 1;
            for (int i = 0; i < maxDataLen; i++)
            {
                for (int j = 0; j < numBlocks; j++)
                {
                    if (i == shortBlockLen - blockEccLen && j < numShortBlocks)
                        continue;
                    result[pos++] = blocks[j][i];
                }
            }

            for (int i = 0; i < blockEccLen; i++)
            {
                for (int j = 0; j < numBlocks; j++)
                {
                    int eccOffset = blocks[j].Length - blockEccLen + i;
                    result[pos++] = blocks[j][eccOffset];
                }
            }

            return result;
        }

        private static byte[] ReedSolomonComputeDivisor(int degree)
        {
            byte[] result = new byte[degree];
            result[degree - 1] = 1;

            byte root = 1;
            for (int i = 0; i < degree; i++)
            {
                for (int j = 0; j < degree; j++)
                {
                    result[j] = ReedSolomonMultiply(result[j], root);
                    if (j + 1 < degree)
                    {
                        result[j] ^= result[j + 1];
                    }
                }
                root = ReedSolomonMultiply(root, 0x02);
            }
            return result;
        }

        private static byte[] ReedSolomonComputeRemainder(byte[] data, byte[] divisor)
        {
            byte[] result = new byte[divisor.Length];
            for (int i = 0; i < data.Length; i++)
            {
                byte factor = (byte)(data[i] ^ result[0]);
                Array.Copy(result, 1, result, 0, result.Length - 1);
                result[result.Length - 1] = 0;
                for (int j = 0; j < result.Length; j++)
                {
                    result[j] ^= ReedSolomonMultiply(divisor[j], factor);
                }
            }
            return result;
        }

        private static byte ReedSolomonMultiply(byte x, byte y)
        {
            int z = 0;
            for (int i = 7; i >= 0; i--)
            {
                z = (z << 1) ^ ((z >> 7) * 0x11D);
                z ^= ((y >> i) & 1) * x;
            }
            return (byte)z;
        }

        private static void DrawFunctionPatterns(int ver, int eccOrd, bool[][] modules, bool[][] isFunction)
        {
            int size = modules.Length;

            // Timing patterns
            for (int i = 0; i < size; i++)
            {
                SetFunctionModule(6, i, i % 2 == 0, modules, isFunction);
                SetFunctionModule(i, 6, i % 2 == 0, modules, isFunction);
            }

            // 3 Finder patterns
            DrawFinderPattern(3, 3, modules, isFunction);
            DrawFinderPattern(size - 4, 3, modules, isFunction);
            DrawFinderPattern(3, size - 4, modules, isFunction);

            // Alignment patterns
            int[] alignPatPos = GetAlignmentPatternPositions(ver);
            int numAlign = alignPatPos.Length;
            for (int i = 0; i < numAlign; i++)
            {
                for (int j = 0; j < numAlign; j++)
                {
                    if ((i == 0 && j == 0) || (i == 0 && j == numAlign - 1) || (i == numAlign - 1 && j == 0))
                        continue;
                    DrawAlignmentPattern(alignPatPos[i], alignPatPos[j], modules, isFunction);
                }
            }

            // Format bits dummy reservation
            DrawFormatBits(eccOrd, 0, modules, isFunction);

            // Version bits (V7..40)
            DrawVersion(ver, modules, isFunction);
        }

        private static void DrawFormatBits(int eccOrd, int mask, bool[][] modules, bool[][] isFunction)
        {
            int size = modules.Length;
            int formatBits = GetFormatBits(eccOrd);
            int data = (formatBits << 3) | mask;
            int rem = data;
            for (int i = 0; i < 10; i++)
            {
                rem = (rem << 1) ^ ((rem >> 9) * 0x537);
            }
            int bits = ((data << 10) | rem) ^ 0x5412;

            for (int i = 0; i <= 5; i++)
                SetFunctionModule(8, i, GetBit(bits, i), modules, isFunction);
            SetFunctionModule(8, 7, GetBit(bits, 6), modules, isFunction);
            SetFunctionModule(8, 8, GetBit(bits, 7), modules, isFunction);
            SetFunctionModule(7, 8, GetBit(bits, 8), modules, isFunction);
            for (int i = 9; i < 15; i++)
                SetFunctionModule(14 - i, 8, GetBit(bits, i), modules, isFunction);

            for (int i = 0; i < 8; i++)
                SetFunctionModule(size - 1 - i, 8, GetBit(bits, i), modules, isFunction);
            for (int i = 8; i < 15; i++)
                SetFunctionModule(8, size - 15 + i, GetBit(bits, i), modules, isFunction);
            SetFunctionModule(8, size - 8, true, modules, isFunction); // Dark module cố định
        }

        private static int GetFormatBits(int eccOrd)
        {
            switch (eccOrd)
            {
                case 0: return 1; // L = 01
                case 1: return 0; // M = 00
                case 2: return 3; // Q = 11
                case 3: return 2; // H = 10
                default: return 3;
            }
        }

        private static void DrawVersion(int ver, bool[][] modules, bool[][] isFunction)
        {
            if (ver < 7) return;
            int size = modules.Length;

            int rem = ver;
            for (int i = 0; i < 12; i++)
            {
                rem = (rem << 1) ^ ((rem >> 11) * 0x1F25);
            }
            int bits = (ver << 12) | rem;

            for (int i = 0; i < 18; i++)
            {
                bool bit = GetBit(bits, i);
                int a = size - 11 + i % 3;
                int b = i / 3;
                SetFunctionModule(a, b, bit, modules, isFunction);
                SetFunctionModule(b, a, bit, modules, isFunction);
            }
        }

        private static void DrawFinderPattern(int x, int y, bool[][] modules, bool[][] isFunction)
        {
            int size = modules.Length;
            for (int dy = -4; dy <= 4; dy++)
            {
                for (int dx = -4; dx <= 4; dx++)
                {
                    int dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
                    int xx = x + dx, yy = y + dy;
                    if (xx >= 0 && xx < size && yy >= 0 && yy < size)
                    {
                        SetFunctionModule(xx, yy, dist != 2 && dist != 4, modules, isFunction);
                    }
                }
            }
        }

        private static void DrawAlignmentPattern(int x, int y, bool[][] modules, bool[][] isFunction)
        {
            for (int dy = -2; dy <= 2; dy++)
            {
                for (int dx = -2; dx <= 2; dx++)
                {
                    SetFunctionModule(x + dx, y + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1, modules, isFunction);
                }
            }
        }

        private static void SetFunctionModule(int x, int y, bool isDark, bool[][] modules, bool[][] isFunction)
        {
            modules[y][x] = isDark;
            isFunction[y][x] = true;
        }

        private static int[] GetAlignmentPatternPositions(int ver)
        {
            if (ver == 1) return new int[0];
            int numAlign = ver / 7 + 2;
            int step = (ver == 32) ? 26 : ((ver * 4 + numAlign * 2 + 1) / (numAlign * 2 - 2) * 2);
            int[] result = new int[numAlign];
            result[0] = 6;
            for (int i = result.Length - 1, pos = ver * 4 + 10; i >= 1; i--, pos -= step)
            {
                result[i] = pos;
            }
            return result;
        }

        private static void DrawCodewords(byte[] data, int ver, bool[][] modules, bool[][] isFunction)
        {
            int size = modules.Length;
            int i = 0; // bit index

            for (int right = size - 1; right >= 1; right -= 2)
            {
                if (right == 6) right = 5;
                for (int vert = 0; vert < size; vert++)
                {
                    for (int j = 0; j < 2; j++)
                    {
                        int x = right - j;
                        bool upward = ((right + 1) & 2) == 0;
                        int y = upward ? size - 1 - vert : vert;
                        if (!isFunction[y][x] && i < data.Length * 8)
                        {
                            modules[y][x] = GetBit(data[i >> 3], 7 - (i & 7));
                            i++;
                        }
                    }
                }
            }
        }

        private static void ApplyMask(int mask, bool[][] modules, bool[][] isFunction)
        {
            int size = modules.Length;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool invert;
                    switch (mask)
                    {
                        case 0: invert = (x + y) % 2 == 0; break;
                        case 1: invert = y % 2 == 0; break;
                        case 2: invert = x % 3 == 0; break;
                        case 3: invert = (x + y) % 3 == 0; break;
                        case 4: invert = (x / 3 + y / 2) % 2 == 0; break;
                        case 5: invert = x * y % 2 + x * y % 3 == 0; break;
                        case 6: invert = (x * y % 2 + x * y % 3) % 2 == 0; break;
                        case 7: invert = ((x + y) % 2 + x * y % 3) % 2 == 0; break;
                        default: invert = false; break;
                    }
                    if (!isFunction[y][x] && invert)
                    {
                        modules[y][x] = !modules[y][x];
                    }
                }
            }
        }

        private static long GetPenaltyScore(bool[][] modules)
        {
            int size = modules.Length;
            long result = 0;

            // Rule 1: Các module liên tiếp cùng màu trên hàng/cột
            for (int y = 0; y < size; y++)
            {
                bool runColor = false;
                int runX = 0;
                for (int x = 0; x < size; x++)
                {
                    if (x == 0 || modules[y][x] != runColor)
                    {
                        runColor = modules[y][x];
                        runX = 1;
                    }
                    else
                    {
                        runX++;
                        if (runX == 5) result += 3;
                        else if (runX > 5) result++;
                    }
                }
            }

            for (int x = 0; x < size; x++)
            {
                bool runColor = false;
                int runY = 0;
                for (int y = 0; y < size; y++)
                {
                    if (y == 0 || modules[y][x] != runColor)
                    {
                        runColor = modules[y][x];
                        runY = 1;
                    }
                    else
                    {
                        runY++;
                        if (runY == 5) result += 3;
                        else if (runY > 5) result++;
                    }
                }
            }

            // Rule 2: Khối 2x2 cùng màu
            for (int y = 0; y < size - 1; y++)
            {
                for (int x = 0; x < size - 1; x++)
                {
                    bool color = modules[y][x];
                    if (color == modules[y][x + 1] &&
                        color == modules[y + 1][x] &&
                        color == modules[y + 1][x + 1])
                    {
                        result += 3;
                    }
                }
            }

            // Rule 3 & 4: Tỷ lệ đen/trắng
            int dark = 0;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (modules[y][x]) dark++;
                }
            }
            int total = size * size;
            int k = (Math.Abs(dark * 20 - total * 10) + total - 1) / total - 1;
            if (k > 0) result += k * 10;

            return result;
        }

        private static bool GetBit(int x, int i)
        {
            return ((x >> i) & 1) != 0;
        }
    }
}

