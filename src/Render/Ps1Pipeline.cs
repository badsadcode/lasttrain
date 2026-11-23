using Godot;

namespace LastTrain.Render;

/// <summary>
/// Sets up the PS1 look:
///   SubViewportContainer (stretch, shrink N, nearest filter)  -> 3D renders at window/N resolution
///   ColorRect with ps1_post shader on top                      -> colour quantise + dither
/// The 3D world lives inside the returned SubViewport.
///
/// N is chosen from the window height so the image is always about <see cref="TargetLines"/> pixels tall
/// (16:9 at 1600x900 -> 400x225, 1920x1080 -> 384x216, 2560x1440 -> 427x240).
/// </summary>
public static class Ps1Pipeline
{
    public static readonly Color FogColor = new(0.36f, 0.34f, 0.31f);

    /// <summary>Vertical resolution we aim for. PS1 games were mostly 240 lines.</summary>
    public const float TargetLines = 225f;

    public static SubViewport Create(Node parent)
    {
        var layer = new CanvasLayer { Name = "Ps1Screen", Layer = 0 };
        parent.AddChild(layer);

        var container = new SubViewportContainer
        {
            Name = "LowResContainer",
            Stretch = true,
            TextureFilter = CanvasItem.TextureFilterEnum.Nearest,
            // Input is handled by CabInput from the root viewport, not forwarded.
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        layer.AddChild(container);
        container.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var viewport = new SubViewport
        {
            Name = "LowRes3D",
            OwnWorld3D = true,
            Msaa3D = Viewport.Msaa.Disabled,
            ScreenSpaceAA = Viewport.ScreenSpaceAAEnum.Disabled,
            UseTaa = false,
            AudioListenerEnable3D = true,
        };
        container.AddChild(viewport);

        var post = new ColorRect { Name = "Ps1Post", MouseFilter = Control.MouseFilterEnum.Ignore };
        var material = new ShaderMaterial { Shader = GD.Load<Shader>("res://shaders/ps1_post.gdshader") };
        post.Material = material;
        layer.AddChild(post);
        post.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        // Keep the pixel size matched to the window: fixed lines, chunky pixels at any size.
        void Refit()
        {
            int shrink = Mathf.Max(1, Mathf.RoundToInt(container.Size.Y / TargetLines));
            if (container.StretchShrink != shrink) container.StretchShrink = shrink;
            material.SetShaderParameter("pixel_size", (float)shrink);
        }
        container.Resized += Refit;
        Refit();

        return viewport;
    }

    /// <summary>Keep vertex snapping matched to the actual low-res size (call when the window resizes, or every frame).</summary>
    public static void SyncSnapResolution(SubViewport viewport)
    {
        RenderingServer.GlobalShaderParameterSet("ps1_snap_resolution", (Vector2)viewport.Size);
    }

    public static void AddEnvironment(Node3D world)
    {
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = FogColor,
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.62f, 0.6f, 0.58f),
            AmbientLightEnergy = 0.55f,
            TonemapMode = Godot.Environment.ToneMapper.Linear,
            FogEnabled = true,
            FogMode = Godot.Environment.FogModeEnum.Depth,
            FogLightColor = FogColor,
            FogDensity = 1.0f,
            FogDepthBegin = 20f,
            FogDepthEnd = 150f,
            FogDepthCurve = 1.2f,
        };
        world.AddChild(new WorldEnvironment { Name = "Environment", Environment = env });

        world.AddChild(new DirectionalLight3D
        {
            Name = "Sun",
            LightColor = new Color(1.0f, 0.86f, 0.7f),
            LightEnergy = 0.8f,
            ShadowEnabled = false,
            RotationDegrees = new Vector3(-28, 150, 0),
        });
    }
}
