using System.Collections.Generic;
using Godot;

namespace LastTrain.Render;

/// <summary>
/// Turns an imported model (FBX/glTF scene) into a single <see cref="ArrayMesh"/> suitable for
/// MultiMesh scattering:
///  - all mesh parts merged, with their node transforms baked in;
///  - normalised so the model is <c>targetHeight</c> tall, centred on X/Z, base at y = 0;
///  - each surface's material replaced by a PS1 material of the same albedo colour.
/// Results are cached per (path, height).
/// </summary>
public static class ModelLoader
{
    static readonly Dictionary<(string, float), ArrayMesh> Cache = new();

    public static ArrayMesh LoadPs1Mesh(string path, float targetHeight, float affine = 0.5f)
    {
        if (Cache.TryGetValue((path, targetHeight), out var cached)) return cached;

        var scene = GD.Load<PackedScene>(path).Instantiate<Node3D>();
        var parts = new List<(Mesh mesh, int surface, Transform3D xform, Material? material)>();
        Collect(scene, Transform3D.Identity, parts);

        Aabb bounds = default;
        bool first = true;
        foreach (var p in parts)
        {
            Aabb box = p.xform * p.mesh.GetAabb();
            bounds = first ? box : bounds.Merge(box);
            first = false;
        }

        float scale = bounds.Size.Y > 0.0001f ? targetHeight / bounds.Size.Y : 1f;
        Vector3 centre = bounds.GetCenter();
        var normalise = new Transform3D(Basis.Identity.Scaled(Vector3.One * scale), Vector3.Zero)
                        * new Transform3D(Basis.Identity, new Vector3(-centre.X, -bounds.Position.Y, -centre.Z));

        var result = new ArrayMesh();
        foreach (var p in parts)
        {
            var st = new SurfaceTool();
            st.AppendFrom(p.mesh, p.surface, normalise * p.xform);
            st.Commit(result);
            result.SurfaceSetMaterial(result.GetSurfaceCount() - 1, Ps1Materials.Solid(AlbedoOf(p.material), affine));
        }

        scene.Free();
        Cache[(path, targetHeight)] = result;
        return result;
    }

    static void Collect(Node node, Transform3D parent, List<(Mesh, int, Transform3D, Material?)> parts)
    {
        Transform3D xform = parent;
        if (node is Node3D n3) xform = parent * n3.Transform;

        if (node is MeshInstance3D mi && mi.Mesh != null)
            for (int i = 0; i < mi.Mesh.GetSurfaceCount(); i++)
                parts.Add((mi.Mesh, i, xform, mi.GetActiveMaterial(i)));

        foreach (Node child in node.GetChildren())
            Collect(child, xform, parts);
    }

    static Color AlbedoOf(Material? material) =>
        material is BaseMaterial3D bm ? bm.AlbedoColor : new Color(0.5f, 0.5f, 0.5f);
}
