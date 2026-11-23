using System;
using Godot;
using LastTrain.Render;
using LastTrain.Simulation;

namespace LastTrain.World;

/// <summary>
/// Everything that helps the driver stop at a stop board:
///   - countdown boards (3, 2, 1 bars) at 600 / 400 / 200 m before the stop,
///   - a station platform running up to the stop,
///   - the stop board itself, with a red lamp.
/// All on the right-hand (driver's) side. Boards glow a little so they read through fog.
///
/// Rule of thumb for the player: from full speed, start braking at the 3-board.
/// </summary>
public partial class StopMarkers : Node3D
{
    public static readonly double[] CountdownDistancesM = { 200, 400, 600 }; // index = bars - 1
    const double PlatformBeforeM = 160;
    const double PlatformAfterM = 6;
    const float PlatformSegmentM = 6f;
    const float BoardX = 3.0f;
    const float PlatformCentreX = 3.6f;
    const float PlatformWidth = 3.4f;

    TrackProfile _profile = new();
    Func<float, double, float> _groundHeight = (_, _) => 0f;

    MultiMesh _posts = null!, _stopBoards = null!, _lamps = null!, _platform = null!, _edge = null!;
    readonly MultiMesh[] _countdown = new MultiMesh[3];

    public StopMarkers Init(TrackProfile profile, Func<float, double, float> groundHeight)
    {
        _profile = profile;
        _groundHeight = groundHeight;
        Name = "StopMarkers";
        return this;
    }

    public override void _Ready()
    {
        var post = Ps1Materials.Solid(new Color(0.85f, 0.85f, 0.8f));
        var concrete = Ps1Materials.Textured(Ps1Materials.Speckle(16, new Color(0.55f, 0.53f, 0.5f), new Color(0.45f, 0.43f, 0.4f), 21),
                                             Colors.White, new Vector2(2, 4), affine: 0.3f);
        var edgePaint = Ps1Materials.Glow(new Color(0.9f, 0.85f, 0.6f), 0.35f);

        _posts = Multi("Posts", new BoxMesh { Size = new Vector3(0.12f, 3.2f, 0.12f) }, post, 8);
        _stopBoards = Multi("StopBoards", new BoxMesh { Size = new Vector3(2.0f, 1.4f, 0.08f) }, Ps1Materials.SignGlow(new Color(0.85f, 0.12f, 0.08f), 0.7f), 2);
        _lamps = Multi("StopLamps", new BoxMesh { Size = new Vector3(0.4f, 0.4f, 0.25f) }, Ps1Materials.SignGlow(new Color(1f, 0.15f, 0.1f), 3f), 2);
        for (int i = 0; i < 3; i++)
        {
            // QuadMesh faces +Z: toward the oncoming train (which travels toward -Z).
            _countdown[i] = Multi($"Countdown{i + 1}", new QuadMesh { Size = new Vector2(1.4f, 2.8f) },
                                  Ps1Materials.PaintedSign(Ps1Materials.CountdownBars(i + 1), 0.6f), 2);
        }
        int segments = (int)((PlatformBeforeM + PlatformAfterM) / PlatformSegmentM) + 2;
        _platform = Multi("Platform", new BoxMesh { Size = new Vector3(PlatformWidth, 1.3f, PlatformSegmentM + 0.02f) }, concrete, segments * 2);
        _edge = Multi("PlatformEdge", new BoxMesh { Size = new Vector3(0.25f, 0.04f, PlatformSegmentM + 0.02f) }, edgePaint, segments * 2);
    }

    MultiMesh Multi(string name, Mesh mesh, Material material, int count)
    {
        var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh, InstanceCount = count, VisibleInstanceCount = 0 };
        AddChild(new MultiMeshInstance3D { Name = name, Multimesh = mm, MaterialOverride = material });
        return mm;
    }

    Transform3D Upright(double s, float x, float y) =>
        new(Basis.Identity, new Vector3(x, (float)_profile.HeightAt(s) + y, (float)-s));

    /// <summary>Place markers that fall inside [start, end] (track metres).</summary>
    public void Rebuild(double start, double end)
    {
        int posts = 0, stops = 0, platform = 0;
        var countdowns = new int[3];

        // Any stop board up to 600 m beyond the window can have countdown boards inside it.
        double first = _profile.NextStopBoard(Math.Max(0, start - PlatformAfterM));
        for (double b = first; b < end + CountdownDistancesM[^1]; b += _profile.StopBoardSpacingM)
        {
            if (b >= start && b <= end && stops < _stopBoards.InstanceCount)
            {
                AddPost(b, ref posts);
                _stopBoards.SetInstanceTransform(stops, Upright(b, BoardX, 3.1f));
                _lamps.SetInstanceTransform(stops, Upright(b, BoardX, 4.05f));
                stops++;
            }

            for (int i = 0; i < 3; i++)
            {
                double at = b - CountdownDistancesM[i];
                if (at < start || at > end || countdowns[i] >= _countdown[i].InstanceCount) continue;
                AddPost(at, ref posts);
                _countdown[i].SetInstanceTransform(countdowns[i]++, Upright(at, BoardX, 3.8f));
            }

            for (double p = b - PlatformBeforeM; p < b + PlatformAfterM; p += PlatformSegmentM)
            {
                double c = p + PlatformSegmentM / 2;
                if (c < start || c > end || platform >= _platform.InstanceCount) continue;
                // Platform top sits 0.65 m above rail level; the box extends down into the ground.
                _platform.SetInstanceTransform(platform, Upright(c, PlatformCentreX, 0.0f));
                _edge.SetInstanceTransform(platform, Upright(c, PlatformCentreX - PlatformWidth / 2 + 0.15f, 0.67f));
                platform++;
            }
        }

        _posts.VisibleInstanceCount = posts;
        _stopBoards.VisibleInstanceCount = stops;
        _lamps.VisibleInstanceCount = stops;
        for (int i = 0; i < 3; i++) _countdown[i].VisibleInstanceCount = countdowns[i];
        _platform.VisibleInstanceCount = platform;
        _edge.VisibleInstanceCount = platform;
    }

    void AddPost(double s, ref int posts)
    {
        if (posts >= _posts.InstanceCount) return;
        float ground = _groundHeight(BoardX, s) - (float)_profile.HeightAt(s);
        // Post from the ground up to just under the board.
        float top = 2.4f;
        float height = top - ground;
        var basis = Basis.Identity.Scaled(new Vector3(1, height / 3.2f, 1));
        _posts.SetInstanceTransform(posts++, new Transform3D(basis, new Vector3(BoardX, (float)_profile.HeightAt(s) + ground + height / 2, (float)-s)));
    }
}
