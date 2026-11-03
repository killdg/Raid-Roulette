using System;
using UnityEngine;

// Generates the dirty paper/cardboard textures in memory. No external images,
// downloads, item records or physics colliders are needed for these visuals.
namespace RaidRoulette.TestingClient
{
    internal static class DebrisArt
    {
        internal static Texture2D Texture(int kind)
        {
            const int size = 128;
            var pixels = new Color32[size * size];
            var random = new System.Random(703 + kind);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int noise = random.Next(-12, 13);
                    Color32 c;
                    if (kind == 0)
                    {
                        int stain = (int)(20 * (Math.Sin(x * 0.09 + y * 0.03) * Math.Sin(y * 0.07) + 1));
                        byte paper = (byte)(151 + noise - stain);
                        c = new Color32(paper, (byte)(paper - 10), (byte)(paper - 29), 255);
                        bool mast = y > 108 && y < 118 && x > 9 && x < 119;
                        bool text = y > 12 && y < 101 && y % 5 < 2 && x > 9 && x < 119 && x % 37 < 30 && random.NextDouble() > 0.2;
                        bool photo = x > 12 && x < 58 && y > 62 && y < 96;
                        if (mast || text || photo)
                        {
                            c = new Color32((byte)(45 + noise), (byte)(44 + noise), (byte)(40 + noise), 255);
                        }

                        if ((x < 2 || x > 125 || y < 2 || y > 125) && random.NextDouble() < 0.6)
                        {
                            c.a = 0;
                        }
                    }
                    else if (kind == 1)
                    {
                        c = new Color32((byte)(109 + noise), (byte)(81 + noise), (byte)(49 + noise), (byte)((x < 3 || y < 3) ? 0 : 255));
                    }
                    else
                    {
                        double nx = (x - 64) / 55d;
                        double ny = (y - 64) / 62d;
                        bool inside = nx * nx + ny * ny < 1 && Math.Abs(nx) < 0.85 - 0.5 * Math.Abs(ny);
                        c = new Color32((byte)(95 + noise), (byte)(105 + noise), (byte)(40 + noise), (byte)(inside ? 255 : 0));
                        if (Math.Abs(x - 64) < 2 && inside)
                        {
                            c = new Color32(130, 120, 65, 255);
                        }
                    }

                    pixels[y * size + x] = c;
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "ROT_ProceduralDebris_" + kind,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        internal static float[] WindSamples()
        {
            var data = new float[16000 * 6];
            var random = new System.Random(9182);
            float slow = 0;
            float fast = 0;
            for (int i = 0; i < data.Length; i++)
            {
                float n = (float)(random.NextDouble() * 2 - 1);
                slow = slow * 0.985f + n * 0.015f;
                fast = fast * 0.65f + n * 0.35f;
                data[i] = Mathf.Clamp((slow * 5 + fast * 0.4f) * 0.5f, -1, 1);
            }

            // Fade the loop seam; avoid a pop without runtime buffers or streaming callbacks.
            for (int i = 0; i < 800; i++)
            {
                float gain = i / 800f;
                data[i] *= gain;
                data[data.Length - 1 - i] *= gain;
            }

            return data;
        }
    }
}
