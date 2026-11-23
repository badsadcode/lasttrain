using System;
using Godot;
using LastTrain.Render;
using LastTrain.Simulation;

namespace LastTrain.Train;

/// <summary>
/// Placeholder 4-6-2 locomotive and tender, blocked out from primitives.
/// Origin = rail top level under the cab's front wall; the engine extends toward -Z, the tender toward +Z.
/// Also owns the chimney smoke and safety-valve steam.
/// </summary>
public partial class LocomotiveBody : Node3D
{
    public const float BoilerCentreY = 2.7f;
    public const float BoilerRadius = 0.85f;
    public const float CabFrontZ = -1.25f;

    LocomotiveSim _sim = null!;
    CpuParticles3D _smoke = null!;
    CpuParticles3D _steam = null!;
    readonly Node3D[] _wheels = new Node3D[6];
    double _wheelAngle;

    public LocomotiveBody Init(LocomotiveSim sim)
    {
        _sim = sim;
        Name = "Locomotive";
        return this;
    }

    public override void _Ready()
    {
        var black = Ps1Materials.Solid(new Color(0.12f, 0.12f, 0.13f));
        var smokebox = Ps1Materials.Solid(new Color(0.08f, 0.08f, 0.08f));
        var red = Ps1Materials.Solid(new Color(0.42f, 0.1f, 0.08f));
        var steel = Ps1Materials.Solid(new Color(0.45f, 0.45f, 0.47f));
        var brass = Ps1Materials.Solid(new Color(0.62f, 0.5f, 0.24f));

        float boilerStart = CabFrontZ;
        float boilerEnd = -10.5f;
        float boilerLength = boilerStart - boilerEnd;
        Shapes.CylinderZ(this, BoilerRadius, boilerLength, new Vector3(0, BoilerCentreY, (boilerStart + boilerEnd) / 2), black, 12);
        Shapes.CylinderZ(this, BoilerRadius + 0.03f, 1.4f, new Vector3(0, BoilerCentreY, boilerEnd + 0.6f), smokebox, 12);
        Shapes.CylinderZ(this, 0.6f, 0.05f, new Vector3(0, BoilerCentreY, boilerEnd - 0.12f), steel, 10);
        Shapes.CylinderY(this, 0.24f, 0.7f, new Vector3(0, BoilerCentreY + BoilerRadius + 0.3f, -9.7f), smokebox);
        Shapes.CylinderY(this, 0.36f, 0.5f, new Vector3(0, BoilerCentreY + BoilerRadius + 0.1f, -5.6f), black);
        Shapes.CylinderY(this, 0.3f, 0.4f, new Vector3(0, BoilerCentreY + BoilerRadius + 0.05f, -7.4f), black);
        Shapes.CylinderY(this, 0.07f, 0.3f, new Vector3(0.25f, BoilerCentreY + BoilerRadius + 0.15f, -3.2f), brass, 6); // whistle

        // Running boards and frames
        foreach (float side in new[] { -1f, 1f })
        {
            Shapes.Box(this, new Vector3(0.55f, 0.05f, 9.3f), new Vector3(side * 1.2f, 1.62f, -5.9f), black);
            Shapes.Box(this, new Vector3(0.12f, 0.5f, 10f), new Vector3(side * 0.62f, 1.1f, -5.6f), red);
            Shapes.Box(this, new Vector3(0.45f, 0.7f, 1.3f), new Vector3(side * 1.15f, 1.0f, -9.4f), black); // cylinders
        }

        // Driving wheels (3 per side) and pilot wheels
        float[] driverZ = { -2.6f, -4.7f, -6.8f };
        int w = 0;
        foreach (float side in new[] { -1f, 1f })
        foreach (float z in driverZ)
        {
            var hub = new Node3D { Position = new Vector3(side * 0.8f, 1.0f, z) };
            AddChild(hub);
            Shapes.CylinderX(hub, 1.0f, 0.14f, Vector3.Zero, red, 12);
            Shapes.Box(hub, new Vector3(0.16f, 0.12f, 1.6f), Vector3.Zero, steel); // spoke bar so rotation is visible
            _wheels[w++] = hub;
        }
        foreach (float side in new[] { -1f, 1f })
        {
            Shapes.CylinderX(this, 0.45f, 0.12f, new Vector3(side * 0.8f, 0.45f, -8.6f), black, 8);
            Shapes.CylinderX(this, 0.45f, 0.12f, new Vector3(side * 0.8f, 0.45f, -9.9f), black, 8);
        }

        // Headlamp
        Shapes.Box(this, new Vector3(0.45f, 0.45f, 0.5f), new Vector3(0, BoilerCentreY + 0.95f, -10.6f), black);
        Shapes.Box(this, new Vector3(0.32f, 0.32f, 0.05f), new Vector3(0, BoilerCentreY + 0.95f, -10.87f), Ps1Materials.Glow(new Color(1f, 0.9f, 0.6f), 2.5f));
        AddChild(new SpotLight3D
        {
            Position = new Vector3(0, BoilerCentreY + 0.95f, -11f),
            LightColor = new Color(1f, 0.9f, 0.65f),
            LightEnergy = 3f,
            SpotRange = 90f,
            SpotAngle = 16f,
            ShadowEnabled = false,
        });

        // Tender
        var tenderMat = Ps1Materials.Solid(new Color(0.14f, 0.13f, 0.13f));
        Shapes.Box(this, new Vector3(3.0f, 2.5f, 7.4f), new Vector3(0, 2.35f, 4.95f), tenderMat, "Tender");
        Shapes.Box(this, new Vector3(2.7f, 0.6f, 3.2f), new Vector3(0, 3.75f, 3.0f),
                   Ps1Materials.Textured(Ps1Materials.Speckle(16, new Color(0.08f, 0.08f, 0.09f), new Color(0.2f, 0.2f, 0.22f), 5), Colors.White, new Vector2(3, 3)), "Coal");
        foreach (float side in new[] { -1f, 1f })
        foreach (float z in new[] { 2.4f, 3.6f, 6.4f, 7.6f })
            Shapes.CylinderX(this, 0.45f, 0.12f, new Vector3(side * 0.8f, 0.45f, z), black, 8);

        BuildSmoke();
    }

    void BuildSmoke()
    {
        var puff = Ps1Materials.Puff(16, 2);
        StandardMaterial3D PuffMaterial() => new()
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            VertexColorUseAsAlbedo = true,
            AlbedoTexture = puff,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest,
        };

        var smokeRamp = new Gradient();
        smokeRamp.SetColor(0, new Color(0.15f, 0.14f, 0.14f, 0.9f));
        smokeRamp.SetColor(1, new Color(0.45f, 0.43f, 0.42f, 0f));
        _smoke = new CpuParticles3D
        {
            Name = "Smoke",
            Amount = 90,
            Lifetime = 4.0,
            LocalCoords = false,
            Position = new Vector3(0, BoilerCentreY + BoilerRadius + 0.7f, -9.7f),
            Mesh = new QuadMesh { Size = new Vector2(1.3f, 1.3f), Material = PuffMaterial() },
            Direction = Vector3.Up,
            Spread = 10f,
            InitialVelocityMin = 1.5f,
            InitialVelocityMax = 3f,
            Gravity = new Vector3(0, 0.5f, 0),
            ScaleAmountMin = 0.8f,
            ScaleAmountMax = 2.2f,
            ColorRamp = smokeRamp,
        };
        AddChild(_smoke);

        var steamRamp = new Gradient();
        steamRamp.SetColor(0, new Color(0.95f, 0.95f, 0.95f, 0.9f));
        steamRamp.SetColor(1, new Color(0.9f, 0.9f, 0.9f, 0f));
        _steam = new CpuParticles3D
        {
            Name = "SafetyValveSteam",
            Amount = 40,
            Lifetime = 1.2,
            LocalCoords = false,
            Emitting = false,
            Position = new Vector3(0.2f, BoilerCentreY + BoilerRadius + 0.2f, -4.3f),
            Mesh = new QuadMesh { Size = new Vector2(0.7f, 0.7f), Material = PuffMaterial() },
            Direction = Vector3.Up,
            Spread = 8f,
            InitialVelocityMin = 6f,
            InitialVelocityMax = 9f,
            ScaleAmountMin = 0.6f,
            ScaleAmountMax = 1.5f,
            ColorRamp = steamRamp,
        };
        AddChild(_steam);
    }

    public override void _Process(double delta)
    {
        // Harder work = more vigorous exhaust.
        float work = (float)Math.Clamp(_sim.SteamDemand * 2.5, 0, 1);
        _smoke.InitialVelocityMin = 1.5f + 8f * work;
        _smoke.InitialVelocityMax = 3f + 12f * work;
        _steam.Emitting = _sim.Boiler.SafetyValveLifting;

        // Wheels turn with distance (or spin when slipping).
        double radius = _sim.Spec.DriverDiameterM / 2;
        double surfaceSpeed = _sim.SpeedMs * (_sim.WheelSlip ? 3.0 : 1.0);
        _wheelAngle += surfaceSpeed / radius * delta;
        foreach (var wheel in _wheels)
            wheel.Rotation = new Vector3((float)-_wheelAngle, 0, 0);
    }
}
