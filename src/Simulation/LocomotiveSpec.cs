namespace LastTrain.Simulation;

/// <summary>
/// Static numbers describing one locomotive and the train behind it.
/// Loosely a 1930s American 4-6-2 "Pacific" hauling a light train.
/// These are plausible guesses for feel, NOT researched data (see DESIGN.md).
/// </summary>
public sealed class LocomotiveSpec
{
    /// <summary>Engine plus tender.</summary>
    public double LocomotiveMassKg { get; init; } = 160_000;

    /// <summary>Everything coupled behind the tender.</summary>
    public double TrainMassKg { get; init; } = 300_000;

    /// <summary>Weight carried on the driving wheels; limits how hard they can push before slipping.</summary>
    public double AdhesiveMassKg { get; init; } = 80_000;

    /// <summary>Dry steel on steel, no sand.</summary>
    public double AdhesionCoefficient { get; init; } = 0.25;

    /// <summary>Starting tractive effort at full regulator, full cut-off, full pressure.</summary>
    public double MaxTractiveEffortN { get; init; } = 180_000;

    /// <summary>Caps force at speed so the engine can't produce infinite power.</summary>
    public double MaxPowerW { get; init; } = 1_800_000;

    /// <summary>Longest cut-off the valve gear allows (reverser fully over).</summary>
    public double MaxCutoff { get; init; } = 0.75;

    /// <summary>Boiler working pressure: 200 psi.</summary>
    public double WorkingPressureBar { get; init; } = 13.8;

    public double RollingResistanceCoeff { get; init; } = 0.0018;

    /// <summary>Air drag in N per (m/s)^2 for the whole train.</summary>
    public double AeroDragCoeff { get; init; } = 12.0;

    /// <summary>Deceleration from brakes alone when the brake cylinders are fully applied.</summary>
    public double MaxBrakeDecel { get; init; } = 1.0;

    /// <summary>Driving wheel diameter; used for chuff timing.</summary>
    public double DriverDiameterM { get; init; } = 2.0;

    public double TotalMassKg => LocomotiveMassKg + TrainMassKg;
}
