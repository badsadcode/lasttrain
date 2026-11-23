using System;
using Godot;
using LastTrain.Render;
using LastTrain.Simulation;

namespace LastTrain.Cab;

/// <summary>
/// Visual + clickable lever. The logic (latching, detents) lives in <see cref="LeverModel"/>.
/// The handle rotates about X from pulled-toward-you (min) to pushed-away (max).
/// </summary>
public partial class Lever : Node3D
{
    const float MaxAngleDeg = 35f;
    const float HandleLength = 0.45f;

    public LeverModel Model { get; private set; } = null!;
    Func<LeverModel, string> _describe = m => m.Name;
    Color _knobColor = Colors.Black;
    Node3D _pivot = null!;

    public Lever Init(LeverModel model, Color knobColor, Func<LeverModel, string> describe)
    {
        Model = model;
        _knobColor = knobColor;
        _describe = describe;
        Name = model.Name;
        return this;
    }

    public string Describe() => _describe(Model);

    public override void _Ready()
    {
        var steel = Ps1Materials.Solid(new Color(0.5f, 0.5f, 0.52f));
        var dark = Ps1Materials.Solid(new Color(0.15f, 0.15f, 0.15f));

        // Quadrant (the notched guide plate) sits beside the handle.
        Shapes.Box(this, new Vector3(0.015f, 0.08f, 0.34f), new Vector3(-0.035f, 0.05f, 0), dark);

        _pivot = new Node3D { Name = "Pivot" };
        AddChild(_pivot);
        Shapes.Box(_pivot, new Vector3(0.035f, HandleLength, 0.035f), new Vector3(0, HandleLength / 2, 0), steel);
        _pivot.AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.045f, Height = 0.09f, RadialSegments = 6, Rings = 3 },
            Position = new Vector3(0, HandleLength, 0),
            MaterialOverride = Ps1Materials.Solid(_knobColor),
        });

        // Generous pick volume: levers should be easy to grab at 320x240.
        var pick = new PickTarget { Name = "Pick", Lever = this };
        pick.AddChild(new CollisionShape3D
        {
            Shape = new BoxShape3D { Size = new Vector3(0.14f, HandleLength + 0.1f, 0.14f) },
            Position = new Vector3(0, HandleLength / 2 + 0.03f, 0),
        });
        _pivot.AddChild(pick);

        UpdateVisual();
    }

    public override void _Process(double delta) => UpdateVisual();

    void UpdateVisual()
    {
        float t = (float)Model.Normalized;
        _pivot.RotationDegrees = new Vector3(Mathf.Lerp(MaxAngleDeg, -MaxAngleDeg, t), 0, 0);
    }
}
