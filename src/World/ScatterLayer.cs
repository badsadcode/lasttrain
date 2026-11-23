using System;
using Godot;

namespace LastTrain.World;

/// <summary>
/// A group of weighted mesh variants scattered with one MultiMesh per variant.
/// Usage per rebuild: Begin(), Add(...) many times, End().
/// </summary>
public sealed class ScatterLayer
{
    readonly MultiMesh[] _variants;
    readonly float[] _cumulativeWeights;
    readonly int[] _counts;

    public ScatterLayer(Node parent, string name, (Mesh mesh, float weight)[] variants, int capacityPerVariant)
    {
        _variants = new MultiMesh[variants.Length];
        _cumulativeWeights = new float[variants.Length];
        _counts = new int[variants.Length];

        float total = 0;
        for (int i = 0; i < variants.Length; i++)
        {
            total += variants[i].weight;
            _cumulativeWeights[i] = total;
            var mm = new MultiMesh
            {
                TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                Mesh = variants[i].mesh,
                InstanceCount = capacityPerVariant,
                VisibleInstanceCount = 0,
            };
            _variants[i] = mm;
            parent.AddChild(new MultiMeshInstance3D { Name = $"{name}_{i}", Multimesh = mm });
        }
        for (int i = 0; i < variants.Length; i++) _cumulativeWeights[i] /= total;
    }

    public void Begin() => Array.Clear(_counts);

    /// <summary>Places one instance of a randomly chosen (weighted) variant.</summary>
    public void Add(Random rng, Transform3D transform)
    {
        double pick = rng.NextDouble();
        int v = 0;
        while (v < _cumulativeWeights.Length - 1 && pick > _cumulativeWeights[v]) v++;
        if (_counts[v] >= _variants[v].InstanceCount) return;
        _variants[v].SetInstanceTransform(_counts[v]++, transform);
    }

    public void End()
    {
        for (int i = 0; i < _variants.Length; i++) _variants[i].VisibleInstanceCount = _counts[i];
    }
}
