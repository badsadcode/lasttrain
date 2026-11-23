using System;

namespace LastTrain.Simulation;

/// <summary>
/// Longitudinal dynamics of one steam locomotive and its train, treated as a single mass.
///
/// Controls (set every tick from the cab):
///  - Regulator 0..1: how much steam goes to the cylinders (the throttle).
///  - Reverser -1..1: direction and cut-off. 0 = mid gear (no drive). Further over = longer cut-off:
///    more pull, much more steam. Short cut-off at speed is efficient; that's the skill.
///  - Brake handle 0..1: see <see cref="AirBrake"/>.
///  - Injector 0..1: feeds water into the boiler.
///
/// Distance and speed are along the track; positive = forward.
/// </summary>
public sealed class LocomotiveSim
{
    public const double G = 9.81;

    public LocomotiveSpec Spec { get; }
    public Boiler Boiler { get; }
    public AirBrake Brake { get; }

    // Inputs
    public double Regulator { get; set; }
    public double Reverser { get; set; }
    public double Injector { get; set; }
    public double BrakeHandle { get => Brake.Handle; set => Brake.Handle = value; }

    // State
    public double SpeedMs { get; private set; }
    public double DistanceM { get; private set; }

    // Telemetry (for HUD, audio, tests)
    public double Cutoff { get; private set; }
    public double SteamDemand { get; private set; }
    public double TractiveForceN { get; private set; }
    public double ResistanceN { get; private set; }
    public double BrakeForceN { get; private set; }
    public double GradeForceN { get; private set; }
    public bool WheelSlip { get; private set; }

    public LocomotiveSim(LocomotiveSpec spec, Boiler? boiler = null, AirBrake? brake = null)
    {
        Spec = spec;
        Boiler = boiler ?? new Boiler();
        Brake = brake ?? new AirBrake();
    }

    public void Shovel() => Boiler.AddCoal();

    /// <param name="grade">Track grade at the current position (rise per metre forward).</param>
    public void Step(double dt, double grade)
    {
        if (dt <= 0) return;

        Brake.Step(dt);

        double absV = Math.Abs(SpeedMs);
        double regulator = Math.Clamp(Regulator, 0, 1);
        double reverser = Math.Clamp(Reverser, -1, 1);
        int direction = reverser > 0.02 ? 1 : reverser < -0.02 ? -1 : 0;
        Cutoff = direction == 0 ? 0 : Math.Abs(reverser) * Spec.MaxCutoff;
        double pressureFraction = Math.Clamp(Boiler.PressureBar / Spec.WorkingPressureBar, 0, 1.05);

        // --- Traction ---------------------------------------------------------
        double force = Spec.MaxTractiveEffortN * regulator * MeanEffectiveFraction(Cutoff) * pressureFraction;
        if (Boiler.Priming) force *= 0.6;
        double powerCap = Spec.MaxPowerW * pressureFraction * regulator / Math.Max(absV, 1.0);
        force = Math.Min(force, powerCap);

        double adhesionLimit = Spec.AdhesionCoefficient * Spec.AdhesiveMassKg * G;
        WheelSlip = force > adhesionLimit;
        if (WheelSlip) force = adhesionLimit * 0.7; // spinning wheels grip worse than rolling ones

        TractiveForceN = direction * force;

        // Steam used ~ volume admitted (cut-off) x piston strokes (speed). Slipping wastes steam.
        SteamDemand = regulator * Cutoff * Math.Clamp(0.15 + absV / 20.0, 0, 2) * pressureFraction
                      * (WheelSlip ? 1.8 : 1.0);
        Boiler.Step(dt, SteamDemand, Injector);

        // --- Resistances (always oppose motion) -------------------------------
        double m = Spec.TotalMassKg;
        double rolling = Spec.RollingResistanceCoeff * m * G;
        ResistanceN = rolling + Spec.AeroDragCoeff * absV * absV;
        BrakeForceN = Brake.Cylinder * Spec.MaxBrakeDecel * m;
        GradeForceN = -m * G * grade;

        // --- Integrate --------------------------------------------------------
        double drive = TractiveForceN + GradeForceN;   // signed, along +distance
        double opposing = ResistanceN + BrakeForceN;   // magnitude

        const double standstill = 0.05;
        double newSpeed;
        if (absV < standstill)
        {
            // At rest, friction and brakes hold the train unless the push beats them.
            double breakaway = rolling * 1.5 + BrakeForceN;
            if (Math.Abs(drive) <= breakaway)
            {
                newSpeed = 0;
            }
            else
            {
                double accel = (drive - Math.Sign(drive) * opposing) / m;
                newSpeed = SpeedMs + accel * dt;
            }
        }
        else
        {
            double accel = (drive - Math.Sign(SpeedMs) * opposing) / m;
            newSpeed = SpeedMs + accel * dt;
            // Resistances can stop the train but never push it backwards.
            if (Math.Sign(newSpeed) != Math.Sign(SpeedMs)) newSpeed = 0;
        }

        SpeedMs = newSpeed;
        DistanceM += SpeedMs * dt;
    }

    /// <summary>
    /// Fraction of full-admission mean effective pressure for a given cut-off.
    /// Ideal expansion: steam admitted for fraction c of the stroke, then expands.
    /// Work ~ c(1 + ln(1/c)). Steam used ~ c. So short cut-off gives more work per unit of steam.
    /// </summary>
    public static double MeanEffectiveFraction(double cutoff)
    {
        if (cutoff < 0.05) return 0;
        double c = Math.Min(cutoff, 1.0);
        return c * (1 + Math.Log(1 / c));
    }
}
