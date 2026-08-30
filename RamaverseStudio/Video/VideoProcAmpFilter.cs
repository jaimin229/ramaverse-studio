using System;
using System.Drawing.Imaging;

namespace RamaverseStudio.Video
{
    public static class VideoProcAmpFilter
    {
        public static unsafe void ApplyColorAdjustments(BitmapData bmpData, double brightness, double contrast, double hueDeg, double saturation, double gamma)
        {
            int width = bmpData.Width;
            int height = bmpData.Height;
            int stride = bmpData.Stride;
            byte* scan0 = (byte*)bmpData.Scan0.ToPointer();

            float brightOffset = (float)(brightness * 2.55); // -255 to +255
            float contrastMult = (float)contrast;
            float satMult = (float)saturation;
            float gammaInv = (float)(1.0 / Math.Max(0.01, gamma));
            float hueRad = (float)(hueDeg * Math.PI / 180.0);
            float cosH = (float)Math.Cos(hueRad);
            float sinH = (float)Math.Sin(hueRad);

            // Precomputed gamma table for speed
            byte[] gammaLut = new byte[256];
            for (int i = 0; i < 256; i++)
            {
                float norm = i / 255.0f;
                gammaLut[i] = (byte)Math.Clamp(Math.Pow(norm, gammaInv) * 255.0, 0, 255);
            }

            for (int y = 0; y < height; y++)
            {
                byte* row = scan0 + (y * stride);
                for (int x = 0; x < width; x++)
                {
                    int px = x * 4;
                    float b = row[px];
                    float g = row[px + 1];
                    float r = row[px + 2];
                    byte a = row[px + 3];

                    if (a == 0) continue;

                    // 1. Brightness & Contrast: out = (in - 128) * contrast + 128 + brightness
                    r = (r - 128.0f) * contrastMult + 128.0f + brightOffset;
                    g = (g - 128.0f) * contrastMult + 128.0f + brightOffset;
                    b = (b - 128.0f) * contrastMult + 128.0f + brightOffset;

                    // 2. Saturation
                    if (Math.Abs(satMult - 1.0f) > 0.01f)
                    {
                        float gray = 0.299f * r + 0.587f * g + 0.114f * b;
                        r = gray + (r - gray) * satMult;
                        g = gray + (g - gray) * satMult;
                        b = gray + (b - gray) * satMult;
                    }

                    // 3. Hue Rotation
                    if (Math.Abs(hueDeg) > 0.1)
                    {
                        float u = -0.147f * r - 0.289f * g + 0.436f * b;
                        float v = 0.615f * r - 0.515f * g - 0.100f * b;
                        float yLum = 0.299f * r + 0.587f * g + 0.114f * b;

                        float uRot = u * cosH - v * sinH;
                        float vRot = u * sinH + v * cosH;

                        r = yLum + 1.13983f * vRot;
                        g = yLum - 0.39465f * uRot - 0.58060f * vRot;
                        b = yLum + 2.03211f * uRot;
                    }

                    // 4. Gamma LUT
                    byte rb = (byte)Math.Clamp(r, 0, 255);
                    byte gb = (byte)Math.Clamp(g, 0, 255);
                    byte bb = (byte)Math.Clamp(b, 0, 255);

                    if (Math.Abs(gamma - 1.0) > 0.02)
                    {
                        rb = gammaLut[rb];
                        gb = gammaLut[gb];
                        bb = gammaLut[bb];
                    }

                    row[px] = bb;
                    row[px + 1] = gb;
                    row[px + 2] = rb;
                }
            }
        }
    }
}
