using System;

namespace LastTrain.Simulation;

/// <summary>
/// The logic of a cab lever, without any visuals.
///
/// Levers LATCH: they stay wherever you release them. While dragging, the value
/// sticks to nearby detents (notches) so positions like "closed" or "mid gear"
/// are easy to hit, but you can drag through them.
/// </summary>
public sealed class LeverModel
{
    public string Name { get; }
    public double Min { get; }
    public double Max { get; }
    public double[] Detents { get; }
    /// <summary>How close (as a fraction of full travel) the raw position must be to snap to a detent.</summary>
    public double SnapFraction { get; }

    public double Value { get; private set; }
    public bool Dragging { get; private set; }

    double _raw;

    public LeverModel(string name, double min, double max, double initial,
                      double[]? detents = null, double snapFraction = 0.04)
    {
        if (max <= min) throw new ArgumentException("max must be greater than min");
        Name = name;
        Min = min;
        Max = max;
        Detents = detents ?? Array.Empty<double>();
        SnapFraction = snapFraction;
        Value = Snap(Math.Clamp(initial, min, max));
        _raw = Value;
    }

    public double Range => Max - Min;
    public double Normalized => (Value - Min) / Range;

    public void BeginDrag()
    {
        Dragging = true;
        _raw = Value;
    }

    /// <summary>Move by an amount in value units. Value latches on release.</summary>
    public void Drag(double delta)
    {
        _raw = Math.Clamp(_raw + delta, Min, Max);
        Value = Snap(_raw);
    }

    public void EndDrag()
    {
        Dragging = false;
        _raw = Value;
    }

    /// <summary>Directly set the value (keyboard shortcuts, loading a save).</summary>
    public void Set(double value)
    {
        Value = Snap(Math.Clamp(value, Min, Max));
        _raw = Value;
    }

    double Snap(double v)
    {
        double radius = SnapFraction * Range;
        double best = v;
        double bestDist = double.MaxValue;
        foreach (double d in Detents)
        {
            double dist = Math.Abs(d - v);
            if (dist <= radius && dist < bestDist)
            {
                best = d;
                bestDist = dist;
            }
        }
        return best;
    }
}
