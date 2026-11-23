using System;
using Godot;

namespace LastTrain.Render;

/// <summary>
/// Factory for materials that use the PS1 surface shader, plus a few generated textures.
/// Everything is generated in code for now; real art replaces it later.
/// </summary>
public static class Ps1Materials
{
    static Shader? _surface;
    static Shader Surface => _surface ??= GD.Load<Shader>("res://shaders/ps1_surface.gdshader");

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
