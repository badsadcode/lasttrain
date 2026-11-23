using Godot;
using LastTrain.Render;

namespace LastTrain.Cab;

/// <summary>Round dial gauge facing +Z (toward the driver). Needle sweeps 270 degrees.</summary>
public partial class Gauge : Node3D
{
    const float SweepDeg = 135f;

    float _radius = 0.1f;
    float? _redLine;
    Node3D _needle = null!;

    /// <param name="redLine">Optional 0..1 position for a red mark (e.g. working pressure).</param>
    public Gauge Init(string name, float radius, float? redLine = null)
    {
        Name = name;
        _radius = radius;
        _redLine = redLine;
        return this;
    }

    public override void _Ready()
    {
        Shapes.CylinderZ(this, _radius + 0.012f, 0.03f, new Vector3(0, 0, -0.01f), Ps1Materials.Solid(new Color(0.62f, 0.5f, 0.24f)), 12);
        Shapes.CylinderZ(this, _radius, 0.03f, Vector3.Zero, Ps1Materials.Solid(new Color(0.85f, 0.82f, 0.72f)), 12);

        if (_redLine is float red)
        {
            var mark = new Node3D { RotationDegrees = new Vector3(0, 0, Mathf.Lerp(SweepDeg, -SweepDeg, red)) };
            AddChild(mark);
            Shapes.Box(mark, new Vector3(0.012f, _radius * 0.3f, 0.005f), new Vector3(0, _radius * 0.78f, 0.017f), Ps1Materials.Solid(new Color(0.7f, 0.08f, 0.06f)));
        }

        _needle = new Node3D { Name = "Needle", Position = new Vector3(0, 0, 0.02f) };
        AddChild(_needle);
        Shapes.Box(_needle, new Vector3(0.008f, _radius * 0.85f, 0.005f), new Vector3(0, _radius * 0.38f, 0), Ps1Materials.Solid(new Color(0.08f, 0.08f, 0.08f), snap: 0.5f));
        SetFraction(0);
    }

    public void SetFraction(float f)
    {
        _needle.RotationDegrees = new Vector3(0, 0, Mathf.Lerp(SweepDeg, -SweepDeg, Mathf.Clamp(f, 0, 1)));
    }
}
