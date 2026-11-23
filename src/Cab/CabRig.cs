using System;
using Godot;
using LastTrain.Render;
using LastTrain.Simulation;
using LastTrain.Train;

namespace LastTrain.Cab;

/// <summary>
/// The cab interior: walls and windows, the boiler backhead with gauges and firebox door,
/// the lever console, and the driver's camera. Right-hand drive (driver sits on the right,
/// looking past the boiler through the right front window).
///
/// Coordinates: train origin at rail top under the cab front wall; cab spans z -1.25..+1.05.
/// </summary>
public partial class CabRig : Node3D
{
    public const float FloorY = 1.6f;
    public static readonly Vector3 EyePosition = new(1.05f, 3.15f, 0.3f);

    LocomotiveSim _sim = null!;

    public Camera3D Camera { get; private set; } = null!;
    public Lever Regulator { get; private set; } = null!;
    public Lever Reverser { get; private set; } = null!;
    public Lever Brake { get; private set; } = null!;
    public Lever Injector { get; private set; } = null!;

    Gauge _pressure = null!;
    Gauge _brakeGauge = null!;
    WaterGlass _water = null!;
    ShaderMaterial _fireGlow = null!;
    OmniLight3D _fireLight = null!;
    readonly Random _flicker = new(42);

    public CabRig Init(LocomotiveSim sim)
    {
        _sim = sim;
        Name = "Cab";
        return this;
    }

    public override void _Ready()
    {
        BuildShell();
        BuildBackhead();
        BuildLevers();

        Camera = new Camera3D
        {
            Name = "DriverCamera",
            Position = EyePosition,
            RotationDegrees = new Vector3(-15, 0, 0),
            Fov = 70,
            Near = 0.05f,
            Far = 170f,
            Current = true,
        };
        AddChild(Camera);
    }

    void BuildShell()
    {
        var paint = Ps1Materials.Solid(new Color(0.2f, 0.16f, 0.13f));
        var roofMat = Ps1Materials.Solid(new Color(0.1f, 0.1f, 0.1f));
        var floor = Ps1Materials.Textured(Ps1Materials.Planks(16, new Color(0.4f, 0.3f, 0.2f), 9), Colors.White, new Vector2(2, 2));

        float front = LocomotiveBody.CabFrontZ;
        Shapes.Box(this, new Vector3(3.0f, 0.1f, 2.3f), new Vector3(0, FloorY - 0.05f, -0.1f), floor, "Floor");
        Shapes.Box(this, new Vector3(3.4f, 0.08f, 2.8f), new Vector3(0, 3.98f, -0.1f), roofMat, "Roof");

        foreach (float side in new[] { -1f, 1f })
        {
            // Lower side sheet; above it the side is open (the driver leans out to see ahead).
            Shapes.Box(this, new Vector3(0.06f, 0.95f, 2.3f), new Vector3(side * 1.5f, FloorY + 0.47f, -0.1f), paint);
            Shapes.Box(this, new Vector3(0.08f, 1.45f, 0.1f), new Vector3(side * 1.5f, 3.25f, front + 0.05f), paint);
            Shapes.Box(this, new Vector3(0.08f, 1.45f, 0.1f), new Vector3(side * 1.5f, 3.25f, 1.0f), paint);
        }

        // Front wall ("spectacle plate") with a window each side of the boiler: x in [0.6, 1.4], y in [2.7, 3.5].
        Shapes.Box(this, new Vector3(3.0f, 1.1f, 0.05f), new Vector3(0, 2.15f, front), paint);
        Shapes.Box(this, new Vector3(3.0f, 0.48f, 0.05f), new Vector3(0, 3.74f, front), paint);
        Shapes.Box(this, new Vector3(1.2f, 0.8f, 0.05f), new Vector3(0, 3.1f, front), paint);
        foreach (float side in new[] { -1f, 1f })
            Shapes.Box(this, new Vector3(0.1f, 0.8f, 0.05f), new Vector3(side * 1.45f, 3.1f, front), paint);

        // A crate of coal-dust gloom by the tender for the fireman's side.
        Shapes.Box(this, new Vector3(0.7f, 0.5f, 0.6f), new Vector3(-0.9f, FloorY + 0.25f, 0.6f), Ps1Materials.Solid(new Color(0.25f, 0.2f, 0.15f)));
    }

    void BuildBackhead()
    {
        float front = LocomotiveBody.CabFrontZ;
        float face = front + 0.08f;
        Shapes.CylinderZ(this, LocomotiveBody.BoilerRadius, 0.12f, new Vector3(0, LocomotiveBody.BoilerCentreY, front + 0.02f),
                         Ps1Materials.Solid(new Color(0.16f, 0.15f, 0.15f)), 14);

        // Firebox door: click to fire. Glows with the fire.
        _fireGlow = Ps1Materials.Glow(new Color(1f, 0.38f, 0.08f), 1f);
        Shapes.Box(this, new Vector3(0.56f, 0.44f, 0.03f), new Vector3(0, 2.12f, face - 0.01f), Ps1Materials.Solid(new Color(0.07f, 0.07f, 0.07f)), "FireboxFrame");
        Shapes.Box(this, new Vector3(0.44f, 0.3f, 0.04f), new Vector3(0, 2.12f, face), _fireGlow, "FireboxOpening");
        var firePick = new PickTarget
        {
            Name = "FireboxPick",
            Position = new Vector3(0, 2.12f, face + 0.03f),
            OnClick = () => _sim.Shovel(),
            Hint = () => $"Firebox door - click to fire (F)   fire {_sim.Boiler.FireBed * 100:F0}%",
        };
        firePick.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(0.5f, 0.38f, 0.08f) } });
        AddChild(firePick);

        _fireLight = new OmniLight3D
        {
            Position = new Vector3(0, 2.1f, face + 0.4f),
            LightColor = new Color(1f, 0.55f, 0.25f),
            OmniRange = 3.5f,
            ShadowEnabled = false,
        };
        AddChild(_fireLight);

        _pressure = new Gauge().Init("BoilerPressure", 0.11f, redLine: 200f / 300f);
        _pressure.Position = new Vector3(0.36f, 3.2f, face + 0.02f);
        AddChild(_pressure);
        AddChild(HintArea(_pressure.Position, 0.13f, () => $"Boiler pressure  {_sim.Boiler.PressureBar * 14.5:F0} psi (red line 200)"));

        _brakeGauge = new Gauge().Init("BrakeCylinder", 0.09f);
        _brakeGauge.Position = new Vector3(-0.36f, 3.2f, face + 0.02f);
        AddChild(_brakeGauge);
        AddChild(HintArea(_brakeGauge.Position, 0.11f, () => $"Brake cylinder  {_sim.Brake.Cylinder * 100:F0}%"));

        _water = new WaterGlass { Position = new Vector3(0.62f, 2.42f, face + 0.02f) };
        AddChild(_water);
        AddChild(HintArea(_water.Position + new Vector3(0, 0.15f, 0), 0.1f, () =>
            $"Water glass  {_sim.Boiler.WaterLevel * 100:F0}%" + (_sim.Boiler.CrownSheetExposed ? "  - LOW WATER!" : "")));
    }

    static PickTarget HintArea(Vector3 position, float halfSize, Func<string> hint)
    {
        var area = new PickTarget { Position = position + new Vector3(0, 0, 0.03f), Hint = hint };
        area.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(halfSize * 2, halfSize * 2, 0.05f) } });
        return area;
    }

    void BuildLevers()
    {
        var wood = Ps1Materials.Solid(new Color(0.3f, 0.22f, 0.15f));
        Shapes.Box(this, new Vector3(1.15f, 0.6f, 0.25f), new Vector3(0.95f, FloorY + 0.3f, -0.95f), wood, "LeverConsole");

        float y = FloorY + 0.6f;
        const float z = -0.85f;

        Regulator = AddLever(new LeverModel("Regulator", 0, 1, 0, detents: new[] { 0.0 }, snapFraction: 0.03),
            new Color(0.75f, 0.6f, 0.25f), new Vector3(0.55f, y, z),
            m => $"Regulator  {m.Value * 100:F0}%");

        // Notched quadrant: snaps every 10% so cut-off is easy to set; 0 = mid gear.
        var reverserNotches = new double[21];
        for (int i = 0; i <= 20; i++) reverserNotches[i] = -1 + i * 0.1;
        Reverser = AddLever(new LeverModel("Reverser", -1, 1, 0, reverserNotches, snapFraction: 0.024),
            new Color(0.2f, 0.2f, 0.2f), new Vector3(0.82f, y, z),
            m => m.Value > 0.02 ? $"Reverser  forward, cut-off {m.Value * 75:F0}%"
               : m.Value < -0.02 ? $"Reverser  reverse, cut-off {-m.Value * 75:F0}%"
               : "Reverser  mid gear (no drive)");

        Brake = AddLever(new LeverModel("Brake", 0, 1, AirBrake.Service, AirBrake.Detents, snapFraction: 0.06),
            new Color(0.65f, 0.12f, 0.08f), new Vector3(1.09f, y, z),
            m => $"Train brake  {AirBrake.NameFor(m.Value)}");

        Injector = AddLever(new LeverModel("Injector", 0, 1, 0, detents: new[] { 0.0, 1.0 }, snapFraction: 0.05),
            new Color(0.3f, 0.45f, 0.6f), new Vector3(1.36f, y, z),
            m => $"Injector  {(m.Value < 0.01 ? "off" : $"{m.Value * 100:F0}%")}"
                 + (m.Value >= 0.01 && !_sim.Boiler.InjectorWorking ? " (not picking up - needs pressure)" : ""));
    }

    Lever AddLever(LeverModel model, Color knob, Vector3 position, Func<LeverModel, string> describe)
    {
        var lever = new Lever().Init(model, knob, describe);
        lever.Position = position;
        AddChild(lever);
        return lever;
    }

    public override void _Process(double delta)
    {
        _pressure.SetFraction((float)(_sim.Boiler.PressureBar * 14.5 / 300.0));
        _brakeGauge.SetFraction((float)_sim.Brake.Cylinder);
        _water.SetLevel((float)_sim.Boiler.WaterLevel);

        float fire = (float)_sim.Boiler.FireBed;
        float flicker = 0.85f + (float)_flicker.NextDouble() * 0.3f;
        _fireGlow.SetShaderParameter("emission_energy", (0.1f + 1.1f * fire) * flicker);
        _fireLight.LightEnergy = (0.1f + 1.6f * fire) * flicker;
    }
}
