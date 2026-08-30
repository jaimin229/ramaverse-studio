using System;
using System.Drawing;
using System.Drawing.Imaging;

namespace RamaverseStudio.Video
{
    public static class ChromaKeyFilter
    {
        public static unsafe void ApplyChromaKey(BitmapData bmpData, System.Windows.Media.Color keyColor, double similarity, double smoothness, double spillReduction)
        {
            byte keyR = keyColor.R;
            byte keyG = keyColor.G;
            byte keyB = keyColor.B;

            // Convert Key Color to YCbCr
            float keyY = 0.299f * keyR + 0.587f * keyG + 0.114f * keyB;
            float keyCb = 128 - 0.168736f * keyR - 0.331264f * keyG + 0.5f * keyB;
            float keyCr = 128 + 0.5f * keyR - 0.418688f * keyG - 0.081312f * keyB;

            float simSq = (float)(similarity * 255.0f);
            simSq = simSq * simSq;
            float smoothRange = (float)(Math.Max(0.01, smoothness) * 255.0f);

            int width = bmpData.Width;
            int height = bmpData.Height;
            int stride = bmpData.Stride;
            byte* scan0 = (byte*)bmpData.Scan0.ToPointer();

            for (int y = 0; y < height; y++)
            {
                byte* row = scan0 + (y * stride);
                for (int x = 0; x < width; x++)
                {
                    int px = x * 4;
                    byte b = row[px];
                    byte g = row[px + 1];
                    byte r = row[px + 2];
                    byte a = row[px + 3];

                    if (a == 0) continue;

                    // YCbCr distance
                    float cb = 128 - 0.168736f * r - 0.331264f * g + 0.5f * b;
                    float cr = 128 + 0.5f * r - 0.418688f * g - 0.081312f * b;

                    float dCb = cb - keyCb;
                    float dCr = cr - keyCr;
                    float dist = (float)Math.Sqrt(dCb * dCb + dCr * dCr);

                    float alphaFactor = 1.0f;
                    float simVal = (float)(similarity * 150.0f);

                    if (dist < simVal)
                    {
                        alphaFactor = 0.0f;
                    }
                    else if (dist < simVal + smoothRange)
                    {
                        alphaFactor = (dist - simVal) / smoothRange;
                    }

                    // Spill reduction (desaturate green/blue halo)
                    if (spillReduction > 0.05 && alphaFactor > 0.0f)
                    {
                        if (keyG > keyR && keyG > keyB) // Green key
                        {
                            float maxRB = Math.Max(r, b);
                            if (g > maxRB)
                            {
                                float spill = (g - maxRB) * (float)spillReduction;
                                g = (byte)Math.Clamp(g - spill, 0, 255);
                                row[px + 1] = g;
                            }
                        }
                        else if (keyB > keyR && keyB > keyG) // Blue key
                        {
                            float maxRG = Math.Max(r, g);
                            if (b > maxRG)
                            {
                                float spill = (b - maxRG) * (float)spillReduction;
                                b = (byte)Math.Clamp(b - spill, 0, 255);
                                row[px] = b;
                            }
                        }
                    }

                    row[px + 3] = (byte)(a * alphaFactor);
                }
            }
        }
    }
}
