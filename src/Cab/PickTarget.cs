using System;
using Godot;

namespace LastTrain.Cab;

/// <summary>
/// Anything in the cab the mouse can interact with. Either a lever (drag) or a click action.
/// </summary>
public partial class PickTarget : StaticBody3D
{
    public Lever? Lever { get; set; }
    public Action? OnClick { get; set; }
    /// <summary>Shown on hover. If null and this is a lever, the lever describes itself.</summary>
    public Func<string>? Hint { get; set; }

    public string Describe() => Hint?.Invoke() ?? Lever?.Describe() ?? "";
}
