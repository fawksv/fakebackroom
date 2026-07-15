using UnityEngine;

public static class TextureGen
{
    public static Texture2D WallTexture(int size = 512)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        var pixels = new Color[size * size];
        var rand = new System.Random(42);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int i = y * size + x;
                float n = 0;
                n += rand.Next(0, 20) / 255f;
                n += Mathf.PerlinNoise(x * 0.08f, y * 0.08f) * 0.15f;
                n += Mathf.PerlinNoise(x * 0.3f, y * 0.3f) * 0.08f;
                float v = 0.12f + n;
                v = Mathf.Clamp(v, 0.05f, 0.35f);

                // Dark stains
                float stain = Mathf.PerlinNoise(x * 0.02f + 10f, y * 0.02f + 20f);
                if (stain > 0.65f) v -= (stain - 0.65f) * 0.3f;

                // Horizontal panel lines
                if (y % 128 < 2 || y % 128 > 126) v *= 0.5f;
                // Vertical seams
                if (x % 256 < 2 || x % 256 > 254) v *= 0.4f;

                // Rust streaks
                float rust = Mathf.PerlinNoise(x * 0.05f, y * 0.01f + 5f);
                if (rust > 0.7f)
                {
                    float r = 0.3f + (rust - 0.7f);
                    pixels[i] = new Color(r * 0.4f, v * 0.3f, v * 0.2f, 1f);
                }
                else
                {
                    pixels[i] = new Color(v, v * 0.95f, v * 0.9f, 1f);
                }
            }
        }
        tex.SetPixels(pixels);
        tex.Apply(true);
        tex.filterMode = FilterMode.Bilinear;
        return tex;
    }

    public static Texture2D MarbleFloorTexture(int size = 512)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        var pixels = new Color[size * size];
        var rand = new System.Random(7);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int i = y * size + x;
                float baseVal = 0.06f + rand.Next(0, 8) / 255f;
                float vein = Mathf.PerlinNoise(x * 0.04f + 3f, y * 0.04f + 7f);
                float vein2 = Mathf.PerlinNoise(x * 0.12f, y * 0.12f + 15f);

                float v = baseVal;
                if (vein2 > 0.55f)
                {
                    float veinStrength = (vein2 - 0.55f) / 0.45f;
                    float veinWidth = Mathf.Sin(x * 0.15f + y * 0.1f) * 0.5f + 0.5f;
                    if (veinWidth > 0.6f && vein > 0.5f)
                        v = baseVal + veinStrength * 0.15f;
                }

                // Grout lines every 128px
                if (x % 128 < 2 || y % 128 < 2) v *= 0.3f;

                v = Mathf.Clamp(v, 0.02f, 0.2f);
                pixels[i] = new Color(v, v * 1.05f, v * 1.15f, 1f);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply(true);
        tex.filterMode = FilterMode.Bilinear;
        return tex;
    }

    public static Texture2D CeilingTexture(int size = 512)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
        var pixels = new Color[size * size];
        var rand = new System.Random(99);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int i = y * size + x;
                float n = 0.1f;
                n += rand.Next(0, 15) / 255f;
                n += Mathf.PerlinNoise(x * 0.06f, y * 0.06f) * 0.1f;
                n += Mathf.PerlinNoise(x * 0.25f, y * 0.25f) * 0.05f;
                float v = Mathf.Clamp(n, 0.04f, 0.25f);

                // Dark water stains spreading from corners
                float distFromCorner = Mathf.Min(
                    Mathf.Min(x, y),
                    Mathf.Min(size - x, size - y)
                ) / (float)size;
                if (distFromCorner < 0.3f)
                    v *= 0.3f + distFromCorner * 2.3f;

                // Random dark blotches
                float blotch = Mathf.PerlinNoise(x * 0.03f + 50f, y * 0.03f + 60f);
                if (blotch > 0.6f) v *= 0.5f;

                // Panel grid lines
                if (x % 170 < 2 || y % 170 < 2) v *= 0.4f;

                pixels[i] = new Color(v, v * 0.97f, v * 0.93f, 1f);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply(true);
        tex.filterMode = FilterMode.Bilinear;
        return tex;
    }

    public static Texture2D DoorTexture(int w = 256, int h = 512)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
        var pixels = new Color[w * h];
        var rand = new System.Random(33);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                float v = 0.15f + rand.Next(0, 10) / 255f;
                v += Mathf.PerlinNoise(x * 0.1f, y * 0.05f) * 0.08f;

                // Door frame border
                int border = 12;
                if (x < border || x > w - border || y < border || y > h - border)
                    v *= 0.4f;

                // Horizontal panels (3 sections)
                int panelH = (h - border * 2) / 3;
                int panelStart = border + panelH;
                int panelEnd = border + panelH * 2;
                if ((y > panelStart && y < panelStart + 4) || (y > panelEnd && y < panelEnd + 4))
                    v *= 0.5f;

                // Vertical inset lines on each panel
                int inset = 20;
                if ((x > inset && x < inset + 3) || (x > w - inset - 3 && x < w - inset))
                    v *= 0.7f;

                // Rust at bottom
                if (y < 60)
                {
                    float rustFactor = (60 - y) / 60f;
                    float r = 0.2f + rustFactor * 0.15f;
                    float g = v * 0.3f;
                    float b = v * 0.15f;
                    pixels[i] = new Color(r, g, b, 1f);
                    continue;
                }

                // Scratches
                if (rand.Next(0, 500) == 0) v *= 0.3f;

                v = Mathf.Clamp(v, 0.05f, 0.35f);
                pixels[i] = new Color(v, v * 0.92f, v * 0.85f, 1f);
            }
        }
        tex.SetPixels(pixels);
        tex.Apply(true);
        tex.filterMode = FilterMode.Bilinear;
        return tex;
    }
}
