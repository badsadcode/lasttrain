using System;
using System.Collections.Generic;
using Godot;
using LastTrain.Cab;
using LastTrain.Simulation;

namespace LastTrain.Tools;

/// <summary>
/// Automated screenshot run, for checking the look without a human at the controls
/// (CI, or an AI developer in a container). Inactive unless requested:
///
///   godot --path . -- --autoshot=/some/output/dir
///
/// Plays a short scripted scenario, saves PNGs, then quits.
/// </summary>
public partial class AutoShot : Node
{
    string _outDir = "";
    LocomotiveSim _sim = null!;
    CabRig _cab = null!;
    CabInput _input = null!;
    Main _main = null!;
    int _frame;
    readonly Queue<(int frame, Action action)> _steps = new();

    public static void AttachIfRequested(Main main, LocomotiveSim sim, CabRig cab, CabInput input)
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (!arg.StartsWith("--autoshot=")) continue;
            var shot = new AutoShot { Name = "AutoShot", _outDir = arg["--autoshot=".Length..], _sim = sim, _cab = cab, _input = input, _main = main };
            main.AddChild(shot);
            return;
        }
    }

    public override void _Ready()
    {
        DirAccess.MakeDirRecursiveAbsolute(_outDir);
        int f = 30;
        _steps.Enqueue((f, () => Save("01_cab_cold_start")));
        // Positive yaw turns left (Godot: rotation about +Y).
        _steps.Enqueue((f += 5, () => Look(-30, 25)));
        _steps.Enqueue((f += 10, () => Save("02_backhead_and_levers")));
        _steps.Enqueue((f += 5, () => { Look(-12, 0); FastForwardToSpeed(); }));
        _steps.Enqueue((f += 90, () => Save("03_running_ahead")));
        _steps.Enqueue((f += 5, () => { _input.ForceLean = true; Look(-6, 0); }));
        _steps.Enqueue((f += 40, () => Save("04_leaning_out")));
        _steps.Enqueue((f += 5, () => { _input.ForceLean = false; Look(-8, -75); }));
        _steps.Enqueue((f += 30, () => Save("05_side_view")));
        _steps.Enqueue((f += 5, () => GetTree().Quit()));
    }

    public override void _Process(double delta)
    {
        _frame++;
        while (_steps.Count > 0 && _steps.Peek().frame <= _frame)
            _steps.Dequeue().action();
    }

    void Look(float pitch, float yaw) => _cab.Camera.RotationDegrees = new Vector3(pitch, yaw, 0);

    /// <summary>Drive the sim forward ~3 minutes instantly, like a competent driver would.</summary>
    void FastForwardToSpeed()
    {
        _cab.Brake.Model.Set(AirBrake.Release);
        _cab.Reverser.Model.Set(1.0);
        _cab.Regulator.Model.Set(1.0);
        const double dt = 1.0 / 60.0;
        for (int i = 0; i < 60 * 200; i++)
        {
            if (_sim.Boiler.FireBed < 0.85) _sim.Shovel();
            if (i == 60 * 60) _cab.Reverser.Model.Set(0.4);
            _sim.Regulator = _cab.Regulator.Model.Value;
            _sim.Reverser = _cab.Reverser.Model.Value;
            _sim.BrakeHandle = _cab.Brake.Model.Value;
            _sim.Step(dt, 0);
        }
        _main.SnapVisualsTo(_sim.DistanceM);
    }

    void Save(string name)
    {
        string path = $"{_outDir}/{name}.png";
        GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print($"[AutoShot] saved {path}  (speed {_sim.SpeedMs * 2.237:F0} mph, {_sim.Boiler.PressureBar * 14.5:F0} psi)");
    }
}
