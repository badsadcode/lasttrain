using Godot;

namespace LastTrain.Render;

/// <summary>Tiny helpers for blocking out placeholder geometry in code.</summary>
public static class Shapes
{
    public static MeshInstance3D Box(Node parent, Vector3 size, Vector3 position, Material material, string? name = null)
    {
        var mi = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            Position = position,
            MaterialOverride = material,
        };
        if (name != null) mi.Name = name;
        parent.AddChild(mi);
        return mi;
    }

    /// <summary>Cylinder lying along the Z axis (boilers, gauges facing the camera).</summary>
    public static MeshInstance3D CylinderZ(Node parent, float radius, float length, Vector3 position, Material material, int segments = 10)
    {
        var mi = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = length, RadialSegments = segments, Rings = 0 },
            Position = position,
            RotationDegrees = new Vector3(90, 0, 0),
            MaterialOverride = material,
        };
        parent.AddChild(mi);
        return mi;
    }

    /// <summary>Cylinder lying along the X axis (wheels).</summary>
    public static MeshInstance3D CylinderX(Node parent, float radius, float length, Vector3 position, Material material, int segments = 10)
    {
        var mi = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = length, RadialSegments = segments, Rings = 0 },
            Position = position,
            RotationDegrees = new Vector3(0, 0, 90),
            MaterialOverride = material,
        };
        parent.AddChild(mi);
        return mi;
    }

    /// <summary>Upright cylinder (domes, chimney).</summary>
    public static MeshInstance3D CylinderY(Node parent, float radius, float height, Vector3 position, Material material, int segments = 8)
    {
        var mi = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = radius, BottomRadius = radius, Height = height, RadialSegments = segments, Rings = 0 },
            Position = position,
            MaterialOverride = material,
        };
        parent.AddChild(mi);
        return mi;
    }
}
