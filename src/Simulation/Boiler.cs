using System;

namespace LastTrain.Simulation;

/// <summary>
/// A deliberately simple boiler model. Pressure is the "energy store":
/// the fire adds to it, the cylinders and injector take from it.
///
/// Key behaviours we want the player to feel:
///  - Firing raises pressure, but coal burns away.
///  - Working the engine hard (long cut-off at speed) drains pressure faster than the fire can make it.
///  - Exhaust draught makes the fire burn hotter when the engine works (self-regulating, like the real thing).
///  - Water boils away; the injector refills it but costs a little steam and needs pressure to work.
///  - Too much water = priming (water carried into the cylinders, weaker pull).
///  - Too little water over a hot fire = crown sheet exposed = boiler failure if ignored.
/// </summary>
public sealed class Boiler
{
    public const double WorkingPressureBar = 13.8;   // ~200 psi
    public const double SafetyValveBar = 14.2;
    public const double SafetyValveReseatBar = 13.9;

    // Tuning knobs. Rates are per second.
    const double GenerationRate = 0.7;        // bar/s at full fire bed and strong draught
    const double ConsumptionRate = 1.3;       // bar/s at steam demand 1.0
    const double SafetyValveVentRate = 0.8;   // bar/s lost while the valve is lifting
    const double BaseBurn = 0.002;            // fire bed fraction lost per second at idle
    const double DraughtBurn = 0.006;         // extra burn per unit of steam demand
    const double EvaporationPerBar = 0.002;   // water glass fraction boiled off per bar generated
    const double InjectorFillRate = 0.006;    // water glass fraction per second, injector fully on
    const double InjectorSteamUse = 0.08;     // bar/s, injector fully on
    const double InjectorMinPressure = 3.5;   // injectors won't pick up below this
    const double CoalPerShovel = 0.08;        // fire bed fraction per shovelful
    const double ExposedSecondsToFailure = 15;

    public double PressureBar { get; private set; }
    /// <summary>0 = bottom of the gauge glass (crown sheet about to be uncovered), 1 = top.</summary>
    public double WaterLevel { get; private set; }
    /// <summary>0 = fire out, 1 = full, bright fire bed.</summary>
    public double FireBed { get; private set; }

    public bool SafetyValveLifting { get; private set; }
    public bool InjectorWorking { get; private set; }
    public bool Priming => WaterLevel > 0.95;
    public bool CrownSheetExposed => WaterLevel < 0.05;
    public double ExposedSeconds { get; private set; }
    public bool Failed { get; private set; }
    public int ShovelsFired { get; private set; }

    public Boiler(double pressureBar = 3.0, double waterLevel = 0.7, double fireBed = 0.2)
    {
        PressureBar = pressureBar;
        WaterLevel = waterLevel;
        FireBed = fireBed;
    }

    public void AddCoal()
    {
        if (Failed) return;
        FireBed = Math.Min(1.0, FireBed + CoalPerShovel);
        ShovelsFired++;
    }

    /// <param name="steamDemand">Cylinder steam draw, 0 = shut off, ~1 = working very hard.</param>
    /// <param name="injector">Injector valve opening 0..1.</param>
    public void Step(double dt, double steamDemand, double injector)
    {
        if (Failed)
        {
            // Boiler is gone. Whatever pressure is left escapes.
            PressureBar = Math.Max(0, PressureBar - 2.0 * dt);
            InjectorWorking = false;
            SafetyValveLifting = false;
            return;
        }

        steamDemand = Math.Max(0, steamDemand);
        injector = Math.Clamp(injector, 0, 1);

        // Exhaust steam up the chimney pulls air through the fire.
        double draught = 0.55 + 0.45 * Math.Min(1, steamDemand * 2);
        double generation = WaterLevel > 0 ? GenerationRate * FireBed * draught : 0;
        FireBed = Math.Max(0, FireBed - dt * (BaseBurn + DraughtBurn * steamDemand));

        InjectorWorking = injector > 0.01 && PressureBar >= InjectorMinPressure;
        double injectorSteam = InjectorWorking ? InjectorSteamUse * injector : 0;

        PressureBar += (generation - steamDemand * ConsumptionRate - injectorSteam) * dt;

        // Safety valve with a little hysteresis so it lifts and reseats rather than buzzing.
        if (PressureBar >= SafetyValveBar) SafetyValveLifting = true;
        else if (PressureBar <= SafetyValveReseatBar) SafetyValveLifting = false;
        if (SafetyValveLifting) PressureBar -= SafetyValveVentRate * dt;
        PressureBar = Math.Clamp(PressureBar, 0, SafetyValveBar);

        double fill = InjectorWorking ? InjectorFillRate * injector : 0;
        WaterLevel = Math.Clamp(WaterLevel + (fill - generation * EvaporationPerBar) * dt, 0, 1);

        // Crown sheet: low water over a hot fire under pressure. Recoverable if caught quickly.
        if (CrownSheetExposed && FireBed > 0.2 && PressureBar > 5)
        {
            ExposedSeconds += dt;
            if (ExposedSeconds >= ExposedSecondsToFailure) Failed = true;
        }
        else
        {
            ExposedSeconds = Math.Max(0, ExposedSeconds - dt);
        }
    }
}
