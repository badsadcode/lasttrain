using System;

namespace LastTrain.Simulation;

/// <summary>
/// Automatic air brake, heavily simplified. The handle sets a target brake cylinder
/// application; the cylinders take time to fill and longer to release. That lag is the
/// whole point: you have to brake early with a heavy train.
/// </summary>
public sealed class AirBrake
{
    // Handle positions (normalised 0..1). Also used as lever detents.
    public const double Release = 0.0;
    public const double Light = 0.25;
    public const double Service = 0.5;
    public const double FullService = 0.75;
    public const double Emergency = 1.0;

    public static readonly double[] Detents = { Release, Light, Service, FullService, Emergency };
    public static readonly string[] DetentNames = { "Release", "Light", "Service", "Full service", "Emergency" };

    const double ApplyRate = 0.25;          // cylinder fraction per second (4 s to full)
    const double EmergencyApplyRate = 1.0;  // dumps the train line: fast
    const double ReleaseRate = 0.12;        // ~8 s to fully release

    /// <summary>Brake handle position 0..1.</summary>
    public double Handle { get; set; }

    /// <summary>0 = brakes off, 1 = fully applied.</summary>
    public double Cylinder { get; private set; }

    public AirBrake(double handle = 0, double cylinder = 0)
    {
        Handle = handle;
        Cylinder = cylinder;
    }

    public void Step(double dt)
    {
        double target = Math.Clamp(Handle, 0, 1);
        if (Cylinder < target)
        {
            double rate = target >= Emergency - 1e-6 ? EmergencyApplyRate : ApplyRate;
            Cylinder = Math.Min(target, Cylinder + rate * dt);
        }
        else
        {
            Cylinder = Math.Max(target, Cylinder - ReleaseRate * dt);
        }
    }

    public static string NameFor(double handle)
    {
        int best = 0;
        for (int i = 1; i < Detents.Length; i++)
            if (Math.Abs(Detents[i] - handle) < Math.Abs(Detents[best] - handle)) best = i;
        return DetentNames[best];
    }
}
