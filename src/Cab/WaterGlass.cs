using Godot;
using LastTrain.Render;

namespace LastTrain.Cab;

/// <summary>Boiler water gauge glass: a brass frame with a visible water column.</summary>
public partial class WaterGlass : Node3D
{
    const float Height = 0.3f;
    MeshInstance3D _water = null!;

    public override void _Ready()
    {
        Name = "WaterGlass";
        var brass = Ps1Materials.Solid(new Color(0.62f, 0.5f, 0.24f));
        Shapes.Box(this, new Vector3(0.08f, Height + 0.08f, 0.02f), new Vector3(0, Height / 2, -0.01f), brass);
        Shapes.Box(this, new Vector3(0.04f, Height, 0.01f), new Vector3(0, Height / 2, 0.005f), Ps1Materials.Solid(new Color(0.12f, 0.12f, 0.1f)));
        _water = Shapes.Box(this, new Vector3(0.036f, 1f, 0.012f), Vector3.Zero, Ps1Materials.Glow(new Color(0.55f, 0.7f, 0.75f), 0.4f));
        SetLevel(0.5f);
    }

    public void SetLevel(float level)
    {
        float h = Mathf.Max(0.002f, Mathf.Clamp(level, 0, 1) * Height);
        _water.Scale = new Vector3(1, h, 1);
        _water.Position = new Vector3(0, h / 2, 0.012f);
    }
}
