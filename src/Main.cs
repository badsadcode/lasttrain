using System;
using Godot;
using LastTrain.Audio;
using LastTrain.Cab;
using LastTrain.Render;
using LastTrain.Simulation;
using LastTrain.Tools;
using LastTrain.Train;
using LastTrain.UI;
using LastTrain.World;

namespace LastTrain;

/// <summary>
/// Slice 1 entry point. Builds everything in code and wires the cab controls to the simulation.
///
///   Main (root viewport)
///   ├─ Ps1Screen (CanvasLayer): low-res SubViewport + post-process
///   │   └─ LowRes3D (SubViewport)
///   │       └─ World: environment, Track, Train (Locomotive + Cab + camera)
///   ├─ CabInput: mouse picking / dragging / looking
///   ├─ DevHud
///   └─ LocomotiveAudio
/// </summary>
public partial class Main : Node
{
    readonly TrackProfile _profile = new();
    LocomotiveSim _sim = null!;
    SubViewport _viewport = null!;
    Node3D _train = null!;
    TrackScroller _track = null!;
    CabRig _cab = null!;
    CabInput _input = null!;
    DevHud _hud = null!;

    public override void _Ready()
    {
        // Start: fire low, a little pressure, brakes on. The player has to raise steam first.
        _sim = new LocomotiveSim(new LocomotiveSpec(),
            new Boiler(pressureBar: 3.0, waterLevel: 0.7, fireBed: 0.2),
            new AirBrake(AirBrake.Service, AirBrake.Service));

        _viewport = Ps1Pipeline.Create(this);
        var world = new Node3D { Name = "World" };
        _viewport.AddChild(world);
        Ps1Pipeline.AddEnvironment(world);

        _track = new TrackScroller().Init(_profile);
        world.AddChild(_track);

        _train = new Node3D { Name = "Train" };
        world.AddChild(_train);
        _train.AddChild(new LocomotiveBody().Init(_sim));
        _cab = new CabRig().Init(_sim);
        _train.AddChild(_cab);

        _input = new CabInput().Init(_cab, _viewport);
        AddChild(_input);
        _hud = new DevHud().Init(_sim, _profile);
        AddChild(_hud);
        AddChild(new LocomotiveAudio().Init(_sim));

        AutoShot.AttachIfRequested(this, _sim, _cab, _input);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false } key)
        {
            switch (key.Keycode)
            {
                case Key.F: _sim.Shovel(); break;
                case Key.F3: _hud.ShowReadout = !_hud.ShowReadout; break;
                case Key.H: _hud.ShowNotes = !_hud.ShowNotes; break;
                case Key.F11:
                    DisplayServer.WindowSetMode(DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen
                        ? DisplayServer.WindowMode.Windowed : DisplayServer.WindowMode.Fullscreen);
                    break;
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        _sim.Regulator = _cab.Regulator.Model.Value;
        _sim.Reverser = _cab.Reverser.Model.Value;
        _sim.BrakeHandle = _cab.Brake.Model.Value;
        _sim.Injector = _cab.Injector.Model.Value;
        _sim.Step(delta, _profile.GradeAt(_sim.DistanceM));
    }

    public override void _Process(double delta)
    {
        Ps1Pipeline.SyncSnapResolution(_viewport);

        double s = _sim.DistanceM;
        _train.Position = new Vector3(0, (float)_profile.HeightAt(s), (float)-s);
        _train.Rotation = new Vector3((float)Math.Atan(_profile.GradeAt(s)), 0, 0);
        _track.UpdateAround(s);

        _hud.SetHint(_input.HoverText);
    }

    /// <summary>For tools: jump the whole scene to a track position immediately.</summary>
    public void SnapVisualsTo(double s)
    {
        _train.Position = new Vector3(0, (float)_profile.HeightAt(s), (float)-s);
        _train.Rotation = new Vector3((float)Math.Atan(_profile.GradeAt(s)), 0, 0);
        _track.UpdateAround(s);
    }
}
