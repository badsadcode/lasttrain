using System;

namespace LastTrain.Simulation;

/// <summary>
/// Slice 1 test track: a straight line with a flat start, then gentle rolling
/// hills (a sine wave of grade). Distance s is measured along the track in metres.
/// Slice 2 replaces this with the procedural rail network.
/// </summary>
public sealed class TrackProfile
{
    public double FlatStartM { get; init; } = 400;
    /// <summary>Peak grade as a fraction (0.012 = 1.2%, a stiff main-line climb).</summary>
    public double PeakGrade { get; init; } = 0.012;
    public double PeriodM { get; init; } = 3000;

    public double FirstStopBoardM { get; init; } = 800;
    public double StopBoardSpacingM { get; init; } = 1500;

    /// <summary>Rise per metre travelled forward. Positive = uphill going forward.</summary>
    public double GradeAt(double s)
    {
        if (s < FlatStartM) return 0;
        return PeakGrade * Math.Sin(Phase(s));
    }

    /// <summary>Track height in metres; the exact integral of <see cref="GradeAt"/>.</summary>
    public double HeightAt(double s)
    {
        if (s < FlatStartM) return 0;
        return PeakGrade * PeriodM / (2 * Math.PI) * (1 - Math.Cos(Phase(s)));
    }

    /// <summary>Position of the next stop board at or ahead of s.</summary>
    public double NextStopBoard(double s)
    {
        if (s <= FirstStopBoardM) return FirstStopBoardM;
        double n = Math.Ceiling((s - FirstStopBoardM) / StopBoardSpacingM);
        return FirstStopBoardM + n * StopBoardSpacingM;
    }

    /// <summary>Position of the nearest stop board (ahead or behind).</summary>
    public double NearestStopBoard(double s)
    {
        if (s <= FirstStopBoardM) return FirstStopBoardM;
        double n = Math.Round((s - FirstStopBoardM) / StopBoardSpacingM);
        return FirstStopBoardM + n * StopBoardSpacingM;
    }

    double Phase(double s) => 2 * Math.PI * (s - FlatStartM) / PeriodM;
}
