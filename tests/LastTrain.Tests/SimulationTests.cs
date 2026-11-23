using System;
using LastTrain.Simulation;
using Xunit;

namespace LastTrain.Tests;

/// <summary>
/// These mirror the Slice 1 "done when" checklist in DESIGN.md, where physics can check it.
/// They test behaviour (pressure falls, train stops), not exact tuning numbers.
/// </summary>
public class SimulationTests
{
    const double Dt = 1.0 / 60.0;

    static void Run(LocomotiveSim sim, double seconds, double grade = 0, Action<LocomotiveSim>? each = null)
    {
        int steps = (int)Math.Round(seconds / Dt);
        for (int i = 0; i < steps; i++)
        {
            each?.Invoke(sim);
            sim.Step(Dt, grade);
        }
    }

    static LocomotiveSim HotEngine() =>
        new(new LocomotiveSpec(), new Boiler(pressureBar: 13.5, waterLevel: 0.7, fireBed: 0.9));

    [Fact]
    public void FiringFromColdBuildsPressureToWorkingWithinTwoMinutes()
    {
        var sim = new LocomotiveSim(new LocomotiveSpec()); // default cold-ish start
        double t = 0;
        while (sim.Boiler.PressureBar < 13.0 && t < 120)
        {
            if (sim.Boiler.FireBed < 0.8) sim.Shovel();
            sim.Step(Dt, 0);
            t += Dt;
        }
        Assert.True(sim.Boiler.PressureBar >= 13.0, $"pressure only {sim.Boiler.PressureBar:F1} bar after {t:F0}s");
    }

    [Fact]
    public void OpeningRegulatorInForwardGearMovesTrainForward()
    {
        var sim = HotEngine();
        sim.Reverser = 1;
        sim.Regulator = 0.6;
        Run(sim, 30);
        Assert.True(sim.SpeedMs > 2, $"speed {sim.SpeedMs:F2} m/s");
        Assert.True(sim.DistanceM > 10);
    }

    [Fact]
    public void ReverseGearMovesTrainBackward()
    {
        var sim = HotEngine();
        sim.Reverser = -1;
        sim.Regulator = 0.6;
        Run(sim, 20);
        Assert.True(sim.SpeedMs < -1);
    }

    [Fact]
    public void MidGearProducesNoTraction()
    {
        var sim = HotEngine();
        sim.Reverser = 0;
        sim.Regulator = 1;
        Run(sim, 10);
        Assert.Equal(0, sim.SpeedMs);
    }

    [Fact]
    public void ShortCutoffIsMoreSteamEfficientThanLong()
    {
        double Efficiency(double c) => LocomotiveSim.MeanEffectiveFraction(c) / c;
        Assert.True(Efficiency(0.2) > Efficiency(0.5));
        Assert.True(Efficiency(0.5) > Efficiency(0.75));
    }

    [Fact]
    public void LongCutoffAtSpeedDrainsBoilerButShortCutoffIsSustainable()
    {
        // Get up to speed first, then compare holding long vs short cut-off.
        LocomotiveSim AtSpeed()
        {
            var s = HotEngine();
            s.Reverser = 1; s.Regulator = 1;
            Run(s, 90, each: x => { if (x.Boiler.FireBed < 0.9) x.Shovel(); });
            return s;
        }

        var hard = AtSpeed();
        var easy = AtSpeed();
        Assert.True(hard.SpeedMs > 12, $"didn't reach speed: {hard.SpeedMs:F1}");

        void KeepFiring(LocomotiveSim x) { if (x.Boiler.FireBed < 0.9) x.Shovel(); }

        hard.Reverser = 1.0;   // 75% cut-off
        easy.Reverser = 0.33;  // ~25% cut-off
        Run(hard, 60, each: KeepFiring);
        Run(easy, 60, each: KeepFiring);

        // Even with a full fire, long cut-off can't hold working pressure; short cut-off recovers it.
        Assert.True(hard.Boiler.PressureBar < Boiler.WorkingPressureBar - 3, $"hard running held {hard.Boiler.PressureBar:F1} bar");
        Assert.True(easy.Boiler.PressureBar > Boiler.WorkingPressureBar - 1, $"easy running only {easy.Boiler.PressureBar:F1} bar");
        Assert.True(easy.Boiler.PressureBar > hard.Boiler.PressureBar + 3,
            $"easy {easy.Boiler.PressureBar:F1} vs hard {hard.Boiler.PressureBar:F1}");
    }

    [Fact]
    public void GradientNoticeablySlowsTheTrain()
    {
        LocomotiveSim Drive(double grade)
        {
            var s = HotEngine();
            s.Reverser = 0.5; s.Regulator = 1;
            Run(s, 120, grade, x => { if (x.Boiler.FireBed < 0.9) x.Shovel(); });
            return s;
        }

        double flat = Drive(0).SpeedMs;
        double climb = Drive(0.01).SpeedMs;
        Assert.True(climb < flat * 0.75, $"flat {flat:F1} m/s vs 1% climb {climb:F1} m/s");
    }

    [Fact]
    public void BrakesHaveLag()
    {
        var sim = HotEngine();
        sim.BrakeHandle = AirBrake.FullService;
        Run(sim, 0.5);
        Assert.True(sim.Brake.Cylinder < 0.3, $"cylinder {sim.Brake.Cylinder:F2} after 0.5 s");
        Run(sim, 4);
        Assert.Equal(AirBrake.FullService, sim.Brake.Cylinder, 3);
    }

    [Fact]
    public void FullServiceStopsTrainFromSpeedInReasonableDistance()
    {
        var sim = HotEngine();
        sim.Reverser = 1; sim.Regulator = 1;
        Run(sim, 120, each: x => { if (x.Boiler.FireBed < 0.9) x.Shovel(); });
        double v0 = sim.SpeedMs;
        Assert.True(v0 > 15, $"only reached {v0:F1} m/s");

        sim.Regulator = 0;
        sim.BrakeHandle = AirBrake.FullService;
        double startDist = sim.DistanceM;
        Run(sim, 120);

        Assert.Equal(0, sim.SpeedMs);
        double stopping = sim.DistanceM - startDist;
        Assert.InRange(stopping, 100, 900);
    }

    [Fact]
    public void BrakedTrainHoldsOnGradeAndRollsBackWhenReleased()
    {
        var sim = new LocomotiveSim(new LocomotiveSpec(), new Boiler(), new AirBrake(AirBrake.Service, AirBrake.Service));
        Run(sim, 10, grade: 0.01);
        Assert.Equal(0, sim.SpeedMs);

        sim.BrakeHandle = AirBrake.Release;
        Run(sim, 20, grade: 0.01);
        Assert.True(sim.SpeedMs < -0.5, $"speed {sim.SpeedMs:F2}");
    }

    [Fact]
    public void LettingTheWaterRunLowDestroysTheBoiler()
    {
        var boiler = new Boiler(pressureBar: 13, waterLevel: 0.06, fireBed: 1.0);
        for (int i = 0; i < 60 * 60 && !boiler.Failed; i++) boiler.Step(Dt, steamDemand: 0.5, injector: 0);
        Assert.True(boiler.Failed);
    }

    [Fact]
    public void InjectorRefillsWaterOnlyWithPressure()
    {
        var hot = new Boiler(pressureBar: 12, waterLevel: 0.4, fireBed: 0.6);
        var cold = new Boiler(pressureBar: 2, waterLevel: 0.4, fireBed: 0.0);
        for (int i = 0; i < 60 * 30; i++) { hot.Step(Dt, 0, 1); cold.Step(Dt, 0, 1); }
        Assert.True(hot.WaterLevel > 0.5);
        Assert.False(cold.InjectorWorking);
        Assert.True(cold.WaterLevel <= 0.4);
    }

    [Fact]
    public void SafetyValveLiftsAndCapsPressure()
    {
        var boiler = new Boiler(pressureBar: 13.5, waterLevel: 0.7, fireBed: 1.0);
        bool lifted = false;
        for (int i = 0; i < 60 * 30; i++)
        {
            boiler.Step(Dt, 0, 0);
            lifted |= boiler.SafetyValveLifting;
            Assert.True(boiler.PressureBar <= Boiler.SafetyValveBar + 1e-9);
        }
        Assert.True(lifted);
    }
}

public class LeverModelTests
{
    [Fact]
    public void LatchesWhereReleased()
    {
        var lever = new LeverModel("Regulator", 0, 1, 0);
        lever.BeginDrag();
        lever.Drag(0.37);
        lever.EndDrag();
        Assert.Equal(0.37, lever.Value, 6);
    }

    [Fact]
    public void SnapsToNearbyDetentAndCanBeDraggedThrough()
    {
        var lever = new LeverModel("Brake", 0, 1, 0, AirBrake.Detents, snapFraction: 0.05);
        lever.BeginDrag();
        lever.Drag(0.48);
        Assert.Equal(AirBrake.Service, lever.Value);
        lever.Drag(0.1); // raw 0.58, outside snap radius
        Assert.Equal(0.58, lever.Value, 6);
        lever.EndDrag();
    }

    [Fact]
    public void ClampsToRange()
    {
        var lever = new LeverModel("Reverser", -1, 1, 0);
        lever.BeginDrag();
        lever.Drag(5);
        Assert.Equal(1, lever.Value);
        lever.Drag(-10);
        Assert.Equal(-1, lever.Value);
    }
}

public class TrackProfileTests
{
    [Fact]
    public void HeightIsIntegralOfGrade()
    {
        var p = new TrackProfile();
        double h = 0;
        const double ds = 0.5;
        for (double s = 0; s < 5000; s += ds) h += p.GradeAt(s + ds / 2) * ds;
        Assert.Equal(p.HeightAt(5000), h, 1);
    }

    [Fact]
    public void StopBoardsAreFoundAhead()
    {
        var p = new TrackProfile();
        Assert.Equal(800, p.NextStopBoard(0));
        Assert.Equal(2300, p.NextStopBoard(801));
        Assert.Equal(2300, p.NearestStopBoard(2200));
    }
}
