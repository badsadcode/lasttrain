using System;
using Godot;

namespace LastTrain.Render;

/// <summary>
/// Factory for materials that use the PS1 surface shader, plus a few generated textures.
/// Everything is generated in code for now; real art replaces it later.
/// </summary>
public static class Ps1Materials
{
    static Shader? _surface, _sign;
    static Shader Surface => _surface ??= GD.Load<Shader>("res://shaders/ps1_surface.gdshader");
    static Shader SignShader => _sign ??= GD.Load<Shader>("res://shaders/ps1_sign.gdshader");

    public static ShaderMaterial Solid(Color color, float affine = 0.6f, float snap = 1f)
    {
        var m = new ShaderMaterial { Shader = Surface };
        m.SetShaderParameter("albedo_color", color);
        m.SetShaderParameter("affine_strength", affine);
        m.SetShaderParameter("snap_strength", snap);
        return m;
    }

    public static ShaderMaterial Textured(Texture2D texture, Color tint, Vector2 uvScale, float affine = 0.6f)
    {
        var m = Solid(tint, affine);
        m.SetShaderParameter("albedo_texture", texture);
        m.SetShaderParameter("use_texture", true);
        m.SetShaderParameter("uv_scale", uvScale);
        return m;
    }

    /// <summary>Ground: world-space UVs and per-vertex colour.</summary>
    public static ShaderMaterial Ground(Texture2D texture, float worldUvScale)
    {
        var m = Solid(Colors.White, affine: 0.35f);
        m.SetShaderParameter("albedo_texture", texture);
        m.SetShaderParameter("use_texture", true);
        m.SetShaderParameter("use_vertex_color", true);
        m.SetShaderParameter("world_uv_scale", worldUvScale);
        return m;
    }

    /// <summary>Self-lit surface (lamps, the firebox). Change energy at runtime with SetShaderParameter.</summary>
    public static ShaderMaterial Glow(Color color, float energy)
    {
        var m = Solid(color * 0.3f);
        m.SetShaderParameter("emission_color", color);
        m.SetShaderParameter("emission_energy", energy);
        return m;
    }

    /// <summary>Lineside sign: ignores fog and glows with its texture, so the driver can read it from afar.</summary>
    public static ShaderMaterial PaintedSign(Texture2D texture, float glow)
    {
        var m = Textured(texture, Colors.White, Vector2.One, affine: 0.2f);
        m.Shader = SignShader;
        m.SetShaderParameter("emission_color", Colors.White);
        m.SetShaderParameter("emission_energy", glow);
        m.SetShaderParameter("emission_textured", true);
        return m;
    }

    /// <summary>Solid-colour lineside sign or lamp that ignores fog.</summary>
    public static ShaderMaterial SignGlow(Color color, float energy)
    {
        var m = Glow(color, energy);
        m.Shader = SignShader;
        return m;
    }

    /// <summary>White board with N black bars: railway countdown marker (3 = furthest from the stop).</summary>
    public static ImageTexture CountdownBars(int bars)
    {
        const int w = 16, h = 32;
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgb8);
        img.Fill(new Color(0.92f, 0.9f, 0.84f));
        int band = h / 7;
        for (int i = 0; i < bars; i++)
        {
            int y0 = band * (1 + i * 2);
            for (int y = y0; y < y0 + band; y++)
            for (int x = 0; x < w; x++)
                img.SetPixel(x, y, new Color(0.06f, 0.06f, 0.06f));
        }
        // Thin dark border so the board edge reads at low resolution.
        for (int x = 0; x < w; x++) { img.SetPixel(x, 0, Colors.Black); img.SetPixel(x, h - 1, Colors.Black); }
        for (int y = 0; y < h; y++) { img.SetPixel(0, y, Colors.Black); img.SetPixel(w - 1, y, Colors.Black); }
        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>Two-tone speckle, the cheapest possible "texture".</summary>
    public static ImageTexture Speckle(int size, Color a, Color b, int seed, float bFraction = 0.35f)
    {
        var rng = new Random(seed);
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgb8);
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float t = (float)rng.NextDouble();
            Color c = t < bFraction ? b : a;
            img.SetPixel(x, y, c.Lerp(Colors.Black, (float)rng.NextDouble() * 0.12f));
        }
        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>Planks/boards running along V; for the cab floor and tender.</summary>
    public static ImageTexture Planks(int size, Color baseColor, int seed)
    {
        var rng = new Random(seed);
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgb8);
        int plank = Math.Max(2, size / 8);
        for (int x = 0; x < size; x++)
        {
            float shade = 0.8f + (float)rng.NextDouble() * 0.3f;
            for (int y = 0; y < size; y++)
            {
                bool seam = x % plank == 0;
                float grain = (float)rng.NextDouble() * 0.08f;
                Color c = baseColor * (seam ? 0.45f : shade - grain);
                c.A = 1;
                img.SetPixel(x, y, c);
            }
        }
        return ImageTexture.CreateFromImage(img);
    }

    /// <summary>Soft round puff with hard (nearest-filtered) edges, for smoke and steam.</summary>
    public static ImageTexture Puff(int size, int seed)
    {
        var rng = new Random(seed);
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        float r = size * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = x + 0.5f - r, dy = y + 0.5f - r;
            float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
            float a = d < 0.65f ? 1f : d < 1f ? (rng.NextDouble() > (d - 0.65) / 0.35 ? 1f : 0f) : 0f;
            img.SetPixel(x, y, new Color(1, 1, 1, a));
        }
        return ImageTexture.CreateFromImage(img);
    }
}
