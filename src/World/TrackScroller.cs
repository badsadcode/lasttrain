using System;
using Godot;
using LastTrain.Render;
using LastTrain.Simulation;

namespace LastTrain.World;

/// <summary>
/// Slice 1 world: a straight track along -Z with sleepers, rails, ballast, telegraph poles,
/// stop boards, rolling ground and trees. Only a window around the train exists; it is
/// rebuilt as the train moves (MultiMesh transforms rewritten, ground mesh regenerated).
///
/// Track coordinate s (metres along track) maps to world (x, HeightAt(s) + y, -s).
/// Rail top is at y = 0 relative to the track height.
/// </summary>
public partial class TrackScroller : Node3D
{
    // Window around the train, in metres.
    const float Behind = 40f;
    const float Ahead = 190f;

    const float SleeperSpacing = 0.6f;
    const float SegmentLength = 3f;     // rails and ballast are built from short pitched segments
    const float PoleSpacing = 50f;
    const float GroundCell = 6f;
    const float GroundHalfWidth = 132f;
    const float TreeCell = 10f;
    const int TreesPerCellSide = 3;
    const float RailGaugeHalf = 0.7175f; // standard gauge 1435 mm

    TrackProfile _profile = new();
    readonly FastNoiseLite _hills = new() { Seed = 1930, Frequency = 0.012f, FractalOctaves = 3 };
    readonly FastNoiseLite _patches = new() { Seed = 1931, Frequency = 0.05f };

    MultiMesh _sleepers = null!, _railL = null!, _railR = null!, _ballast = null!;
    MultiMesh _poles = null!, _arms = null!, _boardPosts = null!, _boards = null!;
    MultiMesh _trunks = null!, _canopies = null!;
    MeshInstance3D _ground = null!;
    ShaderMaterial _groundMaterial = null!;

    long _lastSleeperIndex = long.MinValue;
    long _lastGroundIndex = long.MinValue;

    public TrackScroller Init(TrackProfile profile)
    {
        _profile = profile;
        Name = "Track";
        return this;
    }

    public override void _Ready()
    {
        int sleeperCount = (int)((Behind + Ahead) / SleeperSpacing) + 2;
        int segmentCount = (int)((Behind + Ahead) / SegmentLength) + 2;
        int poleCount = (int)((Behind + Ahead) / PoleSpacing) + 2;
        int treeCount = ((int)((Behind + Ahead) / TreeCell) + 2) * TreesPerCellSide * 2;

        var iron = Ps1Materials.Solid(new Color(0.33f, 0.3f, 0.28f), affine: 0.2f);
        var wood = Ps1Materials.Textured(Ps1Materials.Planks(16, new Color(0.32f, 0.24f, 0.17f), 7),
                                         Colors.White, Vector2.One, affine: 0.3f);
        var ballast = Ps1Materials.Textured(Ps1Materials.Speckle(16, new Color(0.42f, 0.4f, 0.37f), new Color(0.27f, 0.26f, 0.25f), 3),
                                            Colors.White, new Vector2(4, 4), affine: 0.3f);
        var pole = Ps1Materials.Solid(new Color(0.3f, 0.23f, 0.17f));
        var board = Ps1Materials.Solid(new Color(0.75f, 0.14f, 0.1f));
        var bark = Ps1Materials.Solid(new Color(0.22f, 0.18f, 0.15f));
        var leaves = Ps1Materials.Solid(new Color(0.17f, 0.24f, 0.15f));

        _sleepers = AddMulti("Sleepers", new BoxMesh { Size = new Vector3(2.6f, 0.16f, 0.24f) }, wood, sleeperCount);
        _railL = AddMulti("RailL", new BoxMesh { Size = new Vector3(0.08f, 0.17f, SegmentLength + 0.02f) }, iron, segmentCount);
        _railR = AddMulti("RailR", new BoxMesh { Size = new Vector3(0.08f, 0.17f, SegmentLength + 0.02f) }, iron, segmentCount);
        _ballast = AddMulti("Ballast", new BoxMesh { Size = new Vector3(3.6f, 0.45f, SegmentLength + 0.02f) }, ballast, segmentCount);
        _poles = AddMulti("Poles", new BoxMesh { Size = new Vector3(0.22f, 7.5f, 0.22f) }, pole, poleCount);
        _arms = AddMulti("Crossarms", new BoxMesh { Size = new Vector3(1.6f, 0.12f, 0.12f) }, pole, poleCount);
        _boardPosts = AddMulti("StopBoardPosts", new BoxMesh { Size = new Vector3(0.12f, 2.4f, 0.12f) }, pole, 3);
        _boards = AddMulti("StopBoards", new BoxMesh { Size = new Vector3(0.9f, 0.6f, 0.06f) }, board, 3);
        _trunks = AddMulti("Trunks", new CylinderMesh { TopRadius = 0.15f, BottomRadius = 0.3f, Height = 3f, RadialSegments = 5, Rings = 0 }, bark, treeCount);
        _canopies = AddMulti("Canopies", new CylinderMesh { TopRadius = 0f, BottomRadius = 2.2f, Height = 6f, RadialSegments = 6, Rings = 0 }, leaves, treeCount);

        var grass = Ps1Materials.Speckle(32, new Color(0.62f, 0.6f, 0.45f), new Color(0.45f, 0.42f, 0.32f), 11, 0.4f);
        _groundMaterial = Ps1Materials.Ground(grass, 0.25f);
        _ground = new MeshInstance3D { Name = "Ground", MaterialOverride = _groundMaterial };
        AddChild(_ground);
    }

    MultiMesh AddMulti(string name, Mesh mesh, Material material, int count)
    {
        var mm = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = mesh,
            InstanceCount = count,
        };
        AddChild(new MultiMeshInstance3D { Name = name, Multimesh = mm, MaterialOverride = material });
        return mm;
    }

    /// <summary>Call every frame with the train's track position.</summary>
    public void UpdateAround(double s)
    {
        long sleeperIndex = (long)Math.Floor(s / SleeperSpacing);
        if (sleeperIndex != _lastSleeperIndex)
        {
            _lastSleeperIndex = sleeperIndex;
            RebuildTrack(s);
        }

        long groundIndex = (long)Math.Floor(s / GroundCell);
        if (groundIndex != _lastGroundIndex)
        {
            _lastGroundIndex = groundIndex;
            RebuildGround(groundIndex * GroundCell);
            RebuildTrees(s);
        }
    }

    // --- Track -----------------------------------------------------------------

    /// <summary>Transform for something lying on the track at s: follows height and pitches with grade.</summary>
    Transform3D OnTrack(double s, float x, float y)
    {
        float pitch = (float)Math.Atan(_profile.GradeAt(s));
        return new Transform3D(new Basis(Vector3.Right, pitch), new Vector3(x, (float)_profile.HeightAt(s) + y, (float)-s));
    }

    /// <summary>Transform for something standing upright beside the track at s.</summary>
    Transform3D Upright(double s, float x, float y) =>
        new(Basis.Identity, new Vector3(x, (float)_profile.HeightAt(s) + y, (float)-s));

    void RebuildTrack(double s)
    {
        double start = s - Behind;

        double s0 = Math.Floor(start / SleeperSpacing) * SleeperSpacing;
        for (int i = 0; i < _sleepers.InstanceCount; i++)
            _sleepers.SetInstanceTransform(i, OnTrack(s0 + i * SleeperSpacing, 0, -0.25f));

        double seg0 = Math.Floor(start / SegmentLength) * SegmentLength;
        for (int i = 0; i < _railL.InstanceCount; i++)
        {
            double c = seg0 + (i + 0.5) * SegmentLength;
            _railL.SetInstanceTransform(i, OnTrack(c, -RailGaugeHalf, -0.085f));
            _railR.SetInstanceTransform(i, OnTrack(c, RailGaugeHalf, -0.085f));
            _ballast.SetInstanceTransform(i, OnTrack(c, 0, -0.52f));
        }

        double p0 = Math.Floor(start / PoleSpacing) * PoleSpacing;
        for (int i = 0; i < _poles.InstanceCount; i++)
        {
            double ps = p0 + i * PoleSpacing;
            float ground = GroundHeight(-4.5f, ps) - (float)_profile.HeightAt(ps);
            _poles.SetInstanceTransform(i, Upright(ps, -4.5f, ground + 3.6f));
            _arms.SetInstanceTransform(i, Upright(ps, -4.5f, ground + 6.9f));
        }

        int boards = 0;
        for (double b = _profile.NextStopBoard(Math.Max(0, start)); b < s + Ahead && boards < _boards.InstanceCount; b += _profile.StopBoardSpacingM)
        {
            _boardPosts.SetInstanceTransform(boards, Upright(b, 2.4f, 0.6f));
            _boards.SetInstanceTransform(boards, Upright(b, 2.4f, 1.75f));
            boards++;
        }
        _boardPosts.VisibleInstanceCount = boards;
        _boards.VisibleInstanceCount = boards;
    }

    // --- Ground and trees -------------------------------------------------------

    /// <summary>Terrain height: flat near the track (formation), rolling hills further out.</summary>
    public float GroundHeight(float x, double s)
    {
        float track = (float)_profile.HeightAt(s) - 0.65f;
        float ax = Mathf.Abs(x);
        float blend = Mathf.SmoothStep(5f, 45f, ax);
        float hills = _hills.GetNoise2D(x, (float)s) * 16f + ax * 0.04f;
        return track + blend * hills;
    }

    void RebuildGround(double s)
    {
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);

        int cols = (int)(GroundHalfWidth * 2 / GroundCell) + 1;
        int rows = (int)((Behind + Ahead + 30) / GroundCell) + 1;
        double sStart = s - Behind - 10;

        var dry = new Color(0.85f, 0.8f, 0.62f);
        var dark = new Color(0.5f, 0.5f, 0.38f);

        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
        {
            float x = -GroundHalfWidth + c * GroundCell;
            double vs = sStart + r * GroundCell;
            float patch = _patches.GetNoise2D(x, (float)vs) * 0.5f + 0.5f;
            st.SetColor(dry.Lerp(dark, patch));
            st.AddVertex(new Vector3(x, GroundHeight(x, vs), (float)-vs));
        }

        for (int r = 0; r < rows - 1; r++)
        for (int c = 0; c < cols - 1; c++)
        {
            int a = r * cols + c, b = a + 1, d = a + cols, e = d + 1;
            // Godot front faces are clockwise as seen by the viewer. Rows advance toward -Z,
            // so (a, d, b) is clockwise when seen from above.
            st.AddIndex(a); st.AddIndex(d); st.AddIndex(b);
            st.AddIndex(b); st.AddIndex(d); st.AddIndex(e);
        }

        st.GenerateNormals();
        _ground.Mesh = st.Commit();
    }

    void RebuildTrees(double s)
    {
        double start = Math.Floor((s - Behind) / TreeCell) * TreeCell;
        int i = 0;
        int max = _trunks.InstanceCount;
        for (double cell = start; i < max; cell += TreeCell)
        {
            long cellId = (long)Math.Round(cell / TreeCell);
            for (int side = -1; side <= 1 && i < max; side += 2)
            for (int k = 0; k < TreesPerCellSide && i < max; k++, i++)
            {
                var rng = new Random(Hash(cellId, side, k));
                float x = side * (9f + (float)rng.NextDouble() * 70f);
                double ts = cell + rng.NextDouble() * TreeCell;
                float y = GroundHeight(x, ts);
                bool dead = rng.NextDouble() < 0.2;   // a few dead trees: something is wrong out here
                float scale = 0.7f + (float)rng.NextDouble() * 0.8f;
                float yaw = (float)rng.NextDouble() * Mathf.Tau;
                var basis = new Basis(Vector3.Up, yaw).Scaled(new Vector3(scale, scale * (dead ? 1.8f : 1f), scale));
                var pos = new Vector3(x, y, (float)-ts);
                _trunks.SetInstanceTransform(i, new Transform3D(basis, pos + basis * new Vector3(0, 1.5f, 0)));
                _canopies.SetInstanceTransform(i, dead
                    ? new Transform3D(Basis.Identity.Scaled(Vector3.One * 0.001f), pos)
                    : new Transform3D(basis, pos + basis * new Vector3(0, 5.5f, 0)));
            }
        }
    }

    static int Hash(long a, int b, int c)
    {
        unchecked
        {
            long h = a * 73856093L ^ b * 19349663L ^ c * 83492791L;
            return (int)(h ^ (h >> 32));
        }
    }
}
