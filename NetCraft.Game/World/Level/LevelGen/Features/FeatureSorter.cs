using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen.Features;

//FeatureSorter 特征排序器对应原版 net.minecraft.world.level.biome.FeatureSorter
//把各群系按 step 引用的已放置特征汇总成「每 step 一个有序列表 + 全局索引映射」
//排序保证同一位置在不同区块视角下的放置顺序一致 否则同一个种子会长出不同地形
public static class FeatureSorter
{
    //StepFeatureData 单步特征数据对应原版 StepFeatureData
    //Features 是按全局索引排好序的列表 IndexOf 给出某个特征在该列表里的下标
    public sealed class StepFeatureData
    {
        private readonly Dictionary<NetCraft.Registry.PlacedFeature, int> _indices;

        public IReadOnlyList<NetCraft.Registry.PlacedFeature> Features { get; }

        public StepFeatureData(IReadOnlyList<NetCraft.Registry.PlacedFeature> features)
        {
            Features = features;
            //按引用比对 原版用的是 identity 查找 元素本身没有值语义
            _indices = new Dictionary<NetCraft.Registry.PlacedFeature, int>(ReferenceEqualityComparer.Instance);
            for (var i = 0; i < features.Count; i++) _indices[features[i]] = i;
        }

        //IndexOf 取特征在本步列表里的下标 未收录返回 -1
        public int IndexOf(NetCraft.Registry.PlacedFeature feature)
            => _indices.TryGetValue(feature, out var index) ? index : -1;
    }

    //NodeKey 排序图节点 同一特征出现在不同 step 视为两个节点 与原版一致
    private readonly record struct NodeKey(int FeatureIndex, int Step);

    //NodeComparer 按先 step 后全局索引排序 与原版 comparatorThenComparingInt 一致
    private sealed class NodeComparer : IComparer<NodeKey>
    {
        public int Compare(NodeKey x, NodeKey y)
        {
            var byStep = x.Step.CompareTo(y.Step);
            return byStep != 0 ? byStep : x.FeatureIndex.CompareTo(y.FeatureIndex);
        }
    }

    //BuildFeaturesPerStep 构建每步特征数据对应原版 buildFeaturesPerStep
    //featureSources 是全部群系 featureGetter 取某群系逐 step 的特征引用
    //返回列表的下标即 step 序数 长度等于所有群系里出现的最大 step 数
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
            //同一群系里前一个特征必须先于后一个放置 这条偏序是排序的唯一依据
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

    //Visit 深度优先拓扑排序 后序收集再整体反转 对应原版 Graph.depthFirstSearch
    //踩到回边说明群系之间存在特征顺序环 这是数据错误必须报出来
    private static void Visit(NodeKey node, SortedDictionary<NodeKey, SortedSet<NodeKey>> edges,
        HashSet<NodeKey> discovered, HashSet<NodeKey> visiting, List<NodeKey> sorted)
    {
        if (discovered.Contains(node)) return;
        if (!visiting.Add(node))
            throw new InvalidOperationException($"群系特征顺序成环 step={node.Step} index={node.FeatureIndex}");
        if (edges.TryGetValue(node, out var successors))
        {
            foreach (var successor in successors) Visit(successor, edges, discovered, visiting, sorted);
        }
        visiting.Remove(node);
        discovered.Add(node);
        sorted.Add(node);
    }
}
