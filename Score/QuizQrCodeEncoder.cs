using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Score
{
    public static class QuizQrCodeEncoder
    {
        public static BitmapSource GenerateQrBitmap(string text, int scale = 7, int quietZone = 4)
        {
            bool[,] matrix = GenerateQrMatrix(text);
            int matrixSize = matrix.GetLength(0);
            int totalDim = (matrixSize + 2 * quietZone) * scale;
            int stride = totalDim * 4;
            byte[] pixelData = new byte[totalDim * stride];

            // Fill white background
            for (int i = 0; i < pixelData.Length; i += 4)
            {
                pixelData[i] = 255;
                pixelData[i + 1] = 255;
                pixelData[i + 2] = 255;
                pixelData[i + 3] = 255;
            }

            // Draw QR modules with high-contrast slate black (#0F172A)
            for (int r = 0; r < matrixSize; r++)
            {
                for (int c = 0; c < matrixSize; c++)
                {
                    if (matrix[r, c])
                    {
                        int startX = (c + quietZone) * scale;
                        int startY = (r + quietZone) * scale;

                        for (int py = 0; py < scale; py++)
                        {
                            for (int px = 0; px < scale; px++)
                            {
                                int idx = ((startY + py) * stride) + ((startX + px) * 4);
                                pixelData[idx] = 0x2A;     // B
                                pixelData[idx + 1] = 0x17; // G
                                pixelData[idx + 2] = 0x0F; // R
                                pixelData[idx + 3] = 0xFF; // A
                            }
                        }
                    }
                }
            }

            var bitmap = BitmapSource.Create(
                totalDim, totalDim,
                96, 96,
                PixelFormats.Bgr32,
                null,
                pixelData,
                stride);
            bitmap.Freeze();
            return bitmap;
        }

        public static bool[,] GenerateQrMatrix(string text)
        {
            byte[] textBytes = Encoding.UTF8.GetBytes(text);
            int version = textBytes.Length <= 42 ? 3 : 4;
            int size = version * 4 + 17;
            int totalCodewords = version == 3 ? 70 : 100;
            int dataCodewords = version == 3 ? 44 : 64;
            int ecCodewords = totalCodewords - dataCodewords;

            var bits = new List<bool>();
            // Mode indicator: 0100 for Byte mode
            bits.Add(false); bits.Add(true); bits.Add(false); bits.Add(false);
            // Character count (8 bits for V1-9 byte mode)
            for (int i = 7; i >= 0; i--) bits.Add(((textBytes.Length >> i) & 1) == 1);
            // Data bits
            foreach (byte b in textBytes)
            {
                for (int i = 7; i >= 0; i--) bits.Add(((b >> i) & 1) == 1);
            }
            // Terminator
            int termLen = Math.Min(4, dataCodewords * 8 - bits.Count);
            for (int i = 0; i < termLen; i++) bits.Add(false);
            // Pad to byte boundary
            while (bits.Count % 8 != 0) bits.Add(false);
            // Pad bytes 0xEC and 0x11
            byte[] pad = { 0xEC, 0x11 };
            int padIdx = 0;
            while (bits.Count < dataCodewords * 8)
            {
                byte pb = pad[padIdx % 2];
                for (int i = 7; i >= 0; i--) bits.Add(((pb >> i) & 1) == 1);
                padIdx++;
            }

            byte[] data = new byte[dataCodewords];
            for (int i = 0; i < dataCodewords; i++)
            {
                byte b = 0;
                for (int j = 0; j < 8; j++)
                {
                    if (bits[i * 8 + j]) b |= (byte)(1 << (7 - j));
                }
                data[i] = b;
            }

            // Galois Field Reed-Solomon EC
            byte[] exp = new byte[512];
            byte[] log = new byte[256];
            int x = 1;
            for (int i = 0; i < 255; i++)
            {
                exp[i] = (byte)x;
                exp[i + 255] = (byte)x;
                log[x] = (byte)i;
                x <<= 1;
                if ((x & 256) != 0) x ^= 0x11D;
            }

            Func<byte, byte, byte> gfMul = (a, b) =>
            {
                if (a == 0 || b == 0) return 0;
                return exp[log[a] + log[b]];
            };

            byte[] gen = { 1 };
            for (int i = 0; i < ecCodewords; i++)
            {
                byte[] next = new byte[gen.Length + 1];
                byte factor = exp[i];
                for (int j = 0; j < gen.Length; j++)
                {
                    next[j] ^= gen[j];
                    next[j + 1] ^= gfMul(gen[j], factor);
                }
                gen = next;
            }

            byte[] rem = new byte[ecCodewords];
            for (int i = 0; i < data.Length; i++)
            {
                byte feedback = (byte)(data[i] ^ rem[0]);
                for (int j = 0; j < ecCodewords - 1; j++)
                {
                    rem[j] = (byte)(rem[j + 1] ^ gfMul(feedback, gen[j + 1]));
                }
                rem[ecCodewords - 1] = gfMul(feedback, gen[ecCodewords]);
            }

            byte[] allCodewords = new byte[totalCodewords];
            Array.Copy(data, 0, allCodewords, 0, dataCodewords);
            Array.Copy(rem, 0, allCodewords, dataCodewords, ecCodewords);

            bool[,] matrix = new bool[size, size];
            bool[,] reserved = new bool[size, size];

            Action<int, int> placeFinder = (r, c) =>
            {
                for (int dr = -1; dr <= 7; dr++)
                {
                    for (int dc = -1; dc <= 7; dc++)
                    {
                        int row = r + dr;
                        int col = c + dc;
                        if (row >= 0 && row < size && col >= 0 && col < size)
                        {
                            reserved[row, col] = true;
                            if (dr >= 0 && dr <= 6 && dc >= 0 && dc <= 6)
                            {
                                bool isBlack = dr == 0 || dr == 6 || dc == 0 || dc == 6 || (dr >= 2 && dr <= 4 && dc >= 2 && dc <= 4);
                                matrix[row, col] = isBlack;
                            }
                            else
                            {
                                matrix[row, col] = false;
                            }
                        }
                    }
                }
            };

            placeFinder(0, 0);
            placeFinder(0, size - 7);
            placeFinder(size - 7, 0);

            // Timing patterns
            for (int i = 8; i < size - 8; i++)
            {
                if (!reserved[6, i])
                {
                    reserved[6, i] = true;
                    matrix[6, i] = (i % 2 == 0);
                }
                if (!reserved[i, 6])
                {
                    reserved[i, 6] = true;
                    matrix[i, 6] = (i % 2 == 0);
                }
            }

            // Alignment pattern
            int alignPos = version == 3 ? 22 : 26;
            for (int dr = -2; dr <= 2; dr++)
            {
                for (int dc = -2; dc <= 2; dc++)
                {
                    int r = alignPos + dr;
                    int c = alignPos + dc;
                    reserved[r, c] = true;
                    matrix[r, c] = Math.Abs(dr) == 2 || Math.Abs(dc) == 2 || (dr == 0 && dc == 0);
                }
            }

            // Dark module
            reserved[size - 8, 8] = true;
            matrix[size - 8, 8] = true;

            // Reserve format info areas
            for (int i = 0; i < 9; i++)
            {
                if (i < size) { reserved[8, i] = true; reserved[i, 8] = true; }
            }
            for (int i = 0; i < 8; i++)
            {
                reserved[8, size - 1 - i] = true;
                reserved[size - 1 - i, 8] = true;
            }

            var allBits = new List<bool>();
            foreach (byte b in allCodewords)
            {
                for (int i = 7; i >= 0; i--) allBits.Add(((b >> i) & 1) == 1);
            }
            while (allBits.Count < totalCodewords * 8) allBits.Add(false);

            int bitIdx = 0;
            int right = size - 1;
            bool upward = true;
            while (right > 0)
            {
                if (right == 6) right--;
                for (int vert = 0; vert < size; vert++)
                {
                    int row = upward ? (size - 1 - vert) : vert;
                    for (int col = right; col >= right - 1; col--)
                    {
                        if (!reserved[row, col])
                        {
                            bool bit = bitIdx < allBits.Count && allBits[bitIdx++];
                            // Apply Mask 0: (row + col) % 2 == 0
                            if ((row + col) % 2 == 0) bit = !bit;
                            matrix[row, col] = bit;
                        }
                    }
                }
                upward = !upward;
                right -= 2;
            }

            // Format info (EC Level M, Mask 0) = 0x5412 (15 bits: f14 down to f0 per ISO/IEC 18004)
            int formatInfo = 0x5412;

            // Copy 1: around top-left finder pattern
            int[] r1 = { 8, 8, 8, 8, 8, 8, 8, 8, 7, 5, 4, 3, 2, 1, 0 };
            int[] c1 = { 0, 1, 2, 3, 4, 5, 7, 8, 8, 8, 8, 8, 8, 8, 8 };
            for (int i = 0; i < 15; i++)
            {
                bool bit = ((formatInfo >> (14 - i)) & 1) == 1; // f14 down to f0
                matrix[r1[i], c1[i]] = bit;
            }

            // Copy 2: bottom-left (f0..f6) and top-right (f7..f14)
            for (int i = 0; i < 7; i++)
            {
                bool bit = ((formatInfo >> i) & 1) == 1; // f0 up to f6
                matrix[size - 1 - i, 8] = bit;
            }
            for (int i = 0; i < 8; i++)
            {
                bool bit = ((formatInfo >> (7 + i)) & 1) == 1; // f7 up to f14
                matrix[8, size - 8 + i] = bit;
            }

            return matrix;
        }
    }
}
