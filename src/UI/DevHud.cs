using System;
using System.Text;
using Godot;
using LastTrain.Simulation;

namespace LastTrain.UI;

/// <summary>
/// Developer readout (F3) plus the hover hint line. Full-resolution text on top of the
/// low-res image. Real 3D gauges replace most of this later. 1930s American units.
/// </summary>
public partial class DevHud : CanvasLayer
{
    LocomotiveSim _sim = null!;
    TrackProfile _profile = null!;
    Label _readout = null!;
    Label _hint = null!;
    Label _notes = null!;
    readonly bool[] _done = new bool[DriverNotes.Length];

    public bool ShowReadout { get; set; } = true;
    public bool ShowNotes { get; set; } = true;

    /// <summary>
    /// How to drive, as a self-ticking checklist. The first steps latch once done;
    /// the "keep" steps are live. Check functions get (sim, profile).
    /// </summary>
    static readonly (string text, bool latches, Func<LocomotiveSim, TrackProfile, bool> check)[] DriverNotes =
    {
        ("Raise steam: fire (F / firebox door) to ~180 psi", true, (s, _) => s.Boiler.PressureBar * 14.5 >= 180),
        ("Reverser fully forward (long cut-off to start)", true, (s, _) => s.Reverser > 0.8 || s.SpeedMs > 3),
        ("Release the brake (red lever all the way back)", true, (s, _) => s.BrakeHandle <= 0.001),
        ("Open the regulator gently (brass knob)", true, (s, _) => s.Regulator > 0.05),
        ("Past 20 mph: notch reverser back to 25-35%", true, (s, _) => s.SpeedMs * 2.237 > 20 && s.Cutoff > 0.18 && s.Cutoff < 0.4),
        ("Keep the water glass half full (injector)", false, (s, _) => s.Boiler.WaterLevel is > 0.4 and < 0.92),
        ("Keep the fire bright: little and often", false, (s, _) => s.Boiler.FireBed > 0.5),
        ("Stop at the red board. From full speed,\n    brake at the 3-bar board", true,
            (s, p) => s.SpeedMs == 0 && Math.Abs(s.DistanceM - p.NearestStopBoard(s.DistanceM)) * 3.28 < 40),
    };

    public DevHud Init(LocomotiveSim sim, TrackProfile profile)
    {
        _sim = sim;
        _profile = profile;
        Name = "DevHud";
        Layer = 5;
        return this;
    }

    public override void _Ready()
    {
        _readout = new Label { Position = new Vector2(12, 10) };
        _readout.AddThemeFontSizeOverride("font_size", 17);
        _readout.AddThemeColorOverride("font_color", new Color(0.95f, 0.9f, 0.75f));
        _readout.AddThemeColorOverride("font_shadow_color", Colors.Black);
        _readout.AddThemeConstantOverride("shadow_offset_x", 2);
        _readout.AddThemeConstantOverride("shadow_offset_y", 2);
        AddChild(_readout);

        _notes = new Label();
        _notes.AddThemeFontSizeOverride("font_size", 17);
        _notes.AddThemeColorOverride("font_color", new Color(0.95f, 0.9f, 0.75f));
        _notes.AddThemeColorOverride("font_shadow_color", Colors.Black);
        _notes.AddThemeConstantOverride("shadow_offset_x", 2);
        _notes.AddThemeConstantOverride("shadow_offset_y", 2);
        AddChild(_notes);
        _notes.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopRight);
        _notes.GrowHorizontal = Control.GrowDirection.Begin;
        _notes.OffsetRight = -14;
        _notes.OffsetTop = 10;

        _hint = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _hint.AddThemeFontSizeOverride("font_size", 20);
        _hint.AddThemeColorOverride("font_color", new Color(1f, 0.95f, 0.8f));
        _hint.AddThemeColorOverride("font_shadow_color", Colors.Black);
        _hint.AddThemeConstantOverride("shadow_offset_x", 2);
        _hint.AddThemeConstantOverride("shadow_offset_y", 2);
        AddChild(_hint);
        _hint.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterBottom);
        _hint.OffsetTop = -48;
        _hint.OffsetBottom = -16;
        _hint.GrowHorizontal = Control.GrowDirection.Both;
    }

    public void SetHint(string text) => _hint.Text = text;

    public override void _Process(double delta)
    {
        UpdateNotes();
        _readout.Visible = ShowReadout;
        if (!ShowReadout) return;

        var b = _sim.Boiler;
        double mph = _sim.SpeedMs * 2.23694;
        double psi = b.PressureBar * 14.5038;
        double grade = _profile.GradeAt(_sim.DistanceM) * 100;
        double d = _sim.DistanceM;
        double toBoardFt = (_profile.NextStopBoard(d) - d) * 3.28084;

        var sb = new StringBuilder();
        sb.AppendLine($"SPEED   {mph,5:F1} mph      GRADE {grade:+0.0;-0.0;0.0}%");
        sb.AppendLine($"BOILER  {psi,5:F0} psi      WATER {b.WaterLevel * 100,3:F0}%   FIRE {b.FireBed * 100,3:F0}%");
        sb.AppendLine($"CUT-OFF {_sim.Cutoff * 100,3:F0}%   REG {_sim.Regulator * 100,3:F0}%   BRAKE {AirBrake.NameFor(_sim.BrakeHandle)} (cyl {_sim.Brake.Cylinder * 100:F0}%)");
        sb.AppendLine($"PULL {_sim.TractiveForceN / 1000,5:F0} kN   DRAG {_sim.ResistanceN / 1000,3:F0} kN   BRAKES {_sim.BrakeForceN / 1000,4:F0} kN");
        sb.AppendLine($"NEXT STOP BOARD  {toBoardFt,6:F0} ft      SHOVELS {b.ShovelsFired}");

        var alerts = new StringBuilder();
        if (b.Failed) alerts.Append("[BOILER FAILED] ");
        else if (b.CrownSheetExposed) alerts.Append($"[LOW WATER! {15 - b.ExposedSeconds:F0}s] ");
        if (b.SafetyValveLifting) alerts.Append("[SAFETY VALVE] ");
        if (b.Priming) alerts.Append("[PRIMING] ");
        if (_sim.WheelSlip) alerts.Append("[WHEELSLIP] ");
        if (b.InjectorWorking) alerts.Append("[INJECTOR] ");
        if (alerts.Length > 0) sb.AppendLine(alerts.ToString());

        // Stopping game: how close did you get to the board?
        double nearest = _profile.NearestStopBoard(d);
        if (_sim.SpeedMs == 0 && Math.Abs(d - nearest) < 60)
        {
            double offFt = (d - nearest) * 3.28084;
            string verdict = Math.Abs(offFt) < 10 ? "spot on!" : Math.Abs(offFt) < 40 ? "not bad" : "keep practising";
            sb.AppendLine($"STOPPED {Math.Abs(offFt):F0} ft {(offFt < 0 ? "short of" : "past")} the board - {verdict}");
        }

        sb.AppendLine();
        sb.Append("F fire   hold E lean out   LMB drag levers   RMB look\nH driver's notes   F3 hide this   F11 fullscreen");
        _readout.Text = sb.ToString();
    }

    void UpdateNotes()
    {
        _notes.Visible = ShowNotes;
        if (!ShowNotes) return;
        var sb = new StringBuilder("DRIVER'S NOTES  (H to hide)\n\n");
        for (int i = 0; i < DriverNotes.Length; i++)
        {
            var (text, latches, check) = DriverNotes[i];
            bool ok = check(_sim, _profile);
            if (latches) { _done[i] |= ok; ok = _done[i]; }
            sb.Append(ok ? "[x] " : "[ ] ").AppendLine(text);
        }
        _notes.Text = sb.ToString();
    }
}
