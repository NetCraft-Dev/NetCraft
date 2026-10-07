using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen.Features;

//FeatureSorter feature sorter, maps to vanilla net.minecraft.world.level.biome.FeatureSorter
//Aggregates the placed features referenced per step across biomes into one ordered list per step plus a global index mapping
//The ordering keeps placement order consistent for the same position across chunk views; otherwise one seed would grow different terrain
public static class FeatureSorter
{
    //StepFeatureData per-step feature data, maps to vanilla StepFeatureData
    //Features is the list ordered by global index; IndexOf gives a feature's index in that list
    public sealed class StepFeatureData
    {
        private readonly Dictionary<NetCraft.Registry.PlacedFeature, int> _indices;

        public IReadOnlyList<NetCraft.Registry.PlacedFeature> Features { get; }

        public StepFeatureData(IReadOnlyList<NetCraft.Registry.PlacedFeature> features)
        {
            Features = features;
            //Compare by reference; vanilla uses identity lookup and the elements have no value semantics
            _indices = new Dictionary<NetCraft.Registry.PlacedFeature, int>(ReferenceEqualityComparer.Instance);
            for (var i = 0; i < features.Count; i++) _indices[features[i]] = i;
        }

        //IndexOf get the feature's index in this step's list; returns -1 when absent
        public int IndexOf(NetCraft.Registry.PlacedFeature feature)
            => _indices.TryGetValue(feature, out var index) ? index : -1;
    }

    //NodeKey sort graph node; the same feature in different steps counts as two nodes, same as vanilla
    private readonly record struct NodeKey(int FeatureIndex, int Step);

    //NodeComparer sorts by step then global index, same as vanilla comparatorThenComparingInt
    private sealed class NodeComparer : IComparer<NodeKey>
    {
        public int Compare(NodeKey x, NodeKey y)
        {
            var byStep = x.Step.CompareTo(y.Step);
            return byStep != 0 ? byStep : x.FeatureIndex.CompareTo(y.FeatureIndex);
        }
    }

    //BuildFeaturesPerStep build the per-step feature data, maps to vanilla buildFeaturesPerStep
    //featureSources is all biomes; featureGetter fetches the per-step feature references of a biome
    //The returned list is indexed by step ordinal and its length equals the largest step seen across all biomes
    public static IReadOnlyList<StepFeatureData> BuildFeaturesPerStep<T>(IReadOnlyList<T> featureSources,
        Func<T, IReadOnlyList<HolderSet<NetCraft.Registry.PlacedFeature>>> featureGetter)
    {
        var comparer = new NodeComparer();
        var indices = new Dictionary<NetCraft.Registry.PlacedFeature, int>(ReferenceEqualityComparer.Instance);
        var nodes = new Dictionary<NodeKey, NetCraft.Registry.PlacedFeature>();
        var edges = new SortedDictionary<NodeKey, SortedSet<NodeKey>>(comparer);
        var nextIndex = 0;
        var maxStep = 0;

        foreach (var source in featureSources)
        {
            var featuresForStep = featureGetter(source);
            if (featuresForStep is null) continue;
            maxStep = Math.Max(maxStep, featuresForStep.Count);

            var ordered = new List<NodeKey>();
            for (var step = 0; step < featuresForStep.Count; step++)
            {
                var featuresInStep = featuresForStep[step];
                if (featuresInStep is null) continue;
                foreach (var holder in featuresInStep)
                {
                    if (!holder.IsBound()) continue;
                    var feature = holder.Value;
                    if (!indices.TryGetValue(feature, out var featureIndex))
                    {
                        featureIndex = nextIndex++;
                        indices[feature] = featureIndex;
                    }
                    var key = new NodeKey(featureIndex, step);
                    nodes[key] = feature;
                    ordered.Add(key);
                }
            }

            foreach (var key in ordered)
            {
                if (!edges.ContainsKey(key)) edges[key] = new SortedSet<NodeKey>(comparer);
            }
            //Within a biome an earlier feature must be placed before a later one; this partial order is the only sorting input
            for (var i = 0; i + 1 < ordered.Count; i++)
                edges[ordered[i]].Add(ordered[i + 1]);
        }

        var sorted = new List<NodeKey>();
        var discovered = new HashSet<NodeKey>();
        var visiting = new HashSet<NodeKey>();
        foreach (var key in edges.Keys) Visit(key, edges, discovered, visiting, sorted);
        sorted.Reverse();

        var result = new List<StepFeatureData>(maxStep);
        for (var step = 0; step < maxStep; step++)
        {
            var stepFeatures = new List<NetCraft.Registry.PlacedFeature>();
            foreach (var key in sorted)
            {
                if (key.Step == step) stepFeatures.Add(nodes[key]);
            }
            result.Add(new StepFeatureData(stepFeatures));
        }
        return result;
    }

    //Visit depth-first topological sort: collect post-order then reverse, maps to vanilla Graph.depthFirstSearch
    //Hitting a back edge means a feature ordering cycle between biomes; that is a data error and must be reported
    private static void Visit(NodeKey node, SortedDictionary<NodeKey, SortedSet<NodeKey>> edges,
        HashSet<NodeKey> discovered, HashSet<NodeKey> visiting, List<NodeKey> sorted)
    {
        if (discovered.Contains(node)) return;
        if (!visiting.Add(node))
            throw new InvalidOperationException($"biome feature ordering has a cycle: step={node.Step} index={node.FeatureIndex}");
        if (edges.TryGetValue(node, out var successors))
        {
            foreach (var successor in successors) Visit(successor, edges, discovered, visiting, sorted);
        }
        visiting.Remove(node);
        discovered.Add(node);
        sorted.Add(node);
    }
}
