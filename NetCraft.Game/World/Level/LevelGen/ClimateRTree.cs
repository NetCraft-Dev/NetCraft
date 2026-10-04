namespace NetCraft.Game.World.Level.LevelGen;

//ClimateRTree 气候参数空间最近邻搜索树对应原版 Climate.RTree
//参数表有几千条 线性遍历会让每个采样点都扫全表 建树后查询只走少量子树
//每个内部节点最多 6 个孩子 建树时挑总包围盒最小的维度分桶 查询时按孩子包围盒距离剪枝
internal sealed class ClimateRTree<T>
{
    //ChildrenPerNode 每个内部节点的孩子数对应原版 CHILDREN_PER_NODE
    private const int ChildrenPerNode = 6;

    //ParameterDimensions 参数空间维度数 6 个气候维度加 offset
    private const int ParameterDimensions = 7;

    private readonly Node _root;

    //上次查询结果作为下次的初始候选 相邻采样点气候相近 命中率很高能省掉大量子树遍历
    //必须每线程一份: 树是全局共享的 生成线程并发查询时共享一份会被互相覆盖
    //候选被覆盖后剪枝起点落在别的线程的目标附近 子树几乎剪不掉 单次搜索从微秒级涨到几十微秒
    //对应原版 RTree 里 lastResult 用 ThreadLocal 的写法
    private readonly ThreadLocal<Leaf?> _lastResult = new();

    private ClimateRTree(Node root) => _root = root;

    //Create 用参数表条目建树 对应原版 RTree.create
    public static ClimateRTree<T> Create(IReadOnlyList<(Climate.ParameterPoint Point, T Value)> values)
    {
        if (values.Count == 0)
            throw new ArgumentException("Need at least one value to build the search tree.");
        var leaves = new List<Node>(values.Count);
        foreach (var (point, value) in values)
            leaves.Add(new Leaf(point, value));
        return new ClimateRTree<T>(Build(ParameterDimensions, leaves));
    }

    //Search 查最近邻 对应原版 RTree.search
    public T Search(Climate.ParameterPoint target)
    {
        var leaf = _root.Search(new ParameterTarget(target), _lastResult.Value);
        _lastResult.Value = leaf;
        return leaf.Value;
    }

    //ParameterTarget 7 维查询值对应原版 RTree.search 里的 long[]
    //做成值类型是为了让它留在栈上 原版靠 Java 逃逸分析消掉这个数组
    //.NET 里它要传给虚方法 Search 逃逸分析用不上 只能显式换成值类型
    private readonly struct ParameterTarget
    {
        public readonly long Temperature;
        public readonly long Humidity;
        public readonly long Continentalness;
        public readonly long Erosion;
        public readonly long Depth;
        public readonly long Weirdness;
        public readonly long Offset;

        //目标点各维都是单值 取 Min 即可
        public ParameterTarget(Climate.ParameterPoint point)
        {
            Temperature = point.Temperature.Min;
            Humidity = point.Humidity.Min;
            Continentalness = point.Continentalness.Min;
            Erosion = point.Erosion.Min;
            Depth = point.Depth.Min;
            Weirdness = point.Weirdness.Min;
            Offset = point.Offset;
        }
    }

    //SpaceOf 参数点展开成 7 维包围盒 offset 按单值区间处理
    private static Climate.Parameter[] SpaceOf(Climate.ParameterPoint point)
        => new[]
        {
            point.Temperature, point.Humidity, point.Continentalness,
            point.Erosion, point.Depth, point.Weirdness, Climate.Parameter.Single(point.Offset)
        };

    //Node 搜索树节点持 7 维包围盒对应原版 RTree.Node
    private abstract class Node
    {
        //Space 用字段而不是属性 热路径每算一次 Distance 都要读它 属性调用在采样里能看见
        public readonly Climate.Parameter[] Space;

        protected Node(IReadOnlyList<Climate.Parameter> space) => Space = space.ToArray();

        //Distance 节点包围盒到目标值的距离 逐维取区间距离再求平方和
        //展开成 7 个表达式而非循环 循环版超出 JIT 内联预算 内联不了就要每个节点付一次调用开销
        public long Distance(in ParameterTarget target)
        {
            return Delta2(target.Temperature, Space[0])
                 + Delta2(target.Humidity, Space[1])
                 + Delta2(target.Continentalness, Space[2])
                 + Delta2(target.Erosion, Space[3])
                 + Delta2(target.Depth, Space[4])
                 + Delta2(target.Weirdness, Space[5])
                 + Delta2(target.Offset, Space[6]);
        }

        //Delta2 单维区间距离的平方 取值落在区间内贡献 0 对应原版 Parameter.distance
        private static long Delta2(long target, in Climate.Parameter span)
        {
            var above = target - span.Max;
            if (above > 0) return above * above;
            var below = span.Min - target;
            return below > 0 ? below * below : 0L;
        }

        public abstract Leaf Search(in ParameterTarget target, Leaf? candidate);
    }

    //Leaf 叶子节点持一个参数表条目对应原版 RTree.Leaf
    private sealed class Leaf : Node
    {
        public T Value { get; }

        public Leaf(Climate.ParameterPoint point, T value) : base(SpaceOf(point)) => Value = value;

        public override Leaf Search(in ParameterTarget target, Leaf? candidate) => this;
    }

    //SubTree 内部节点持若干孩子对应原版 RTree.SubTree
    private sealed class SubTree : Node
    {
        //Children 孩子数组 每次查询都要遍历 存数组才能走索引路径而不是装箱的接口枚举器
        public Node[] Children { get; }

        public SubTree(IReadOnlyList<Node> children)
            : this(BuildParameterSpace(children), children)
        {
        }

        public SubTree(IReadOnlyList<Climate.Parameter> space, IReadOnlyList<Node> children)
            : base(space) => Children = children as Node[] ?? children.ToArray();

        //Search 先算孩子包围盒距离 比当前最优还远的孩子整棵跳过
        public override Leaf Search(in ParameterTarget target, Leaf? candidate)
        {
            var minDistance = candidate is null ? long.MaxValue : candidate.Distance(target);
            var closest = candidate;
            foreach (var child in Children)
            {
                var childDistance = child.Distance(target);
                if (minDistance <= childDistance) continue;
                var leaf = child.Search(target, closest);
                var leafDistance = ReferenceEquals(child, leaf) ? childDistance : leaf.Distance(target);
                if (minDistance <= leafDistance) continue;
                minDistance = leafDistance;
                closest = leaf;
            }
            return closest ?? throw new InvalidOperationException("search tree has no reachable leaf");
        }
    }

    //Build 递归建树对应原版 RTree.build
    //孩子数在一页以内按参数中心幅值和排序 否则逐维度试分桶取总包围盒最小的那个维度
    private static Node Build(int dimensions, List<Node> children)
    {
        if (children.Count == 0)
            throw new InvalidOperationException("Need at least one child to build a node");
        if (children.Count == 1)
            return children[0];
        if (children.Count <= ChildrenPerNode)
        {
            children.Sort((a, b) => Magnitude(a).CompareTo(Magnitude(b)));
            return new SubTree(children);
        }

        var minCost = long.MaxValue;
        var minDimension = -1;
        List<SubTree>? minBuckets = null;
        for (var d = 0; d < dimensions; d++)
        {
            Sort(children, dimensions, d, false);
            var buckets = Bucketize(children);
            var totalCost = 0L;
            foreach (var bucket in buckets) totalCost += Cost(bucket.Space);
            if (minCost <= totalCost) continue;
            minCost = totalCost;
            minDimension = d;
            minBuckets = buckets;
        }

        Sort(minBuckets!, dimensions, minDimension, true);
        var built = new List<Node>(minBuckets!.Count);
        foreach (var bucket in minBuckets)
            built.Add(Build(dimensions, new List<Node>(bucket.Children)));
        return new SubTree(built);
    }

    //Sort 先按指定维度再从该维度起轮转依次比较 对应原版 RTree.sort
    private static void Sort<TNode>(List<TNode> children, int dimensions, int dimension, bool absolute)
        where TNode : Node
    {
        children.Sort((a, b) =>
        {
            for (var d = 0; d < dimensions; d++)
            {
                var axis = (dimension + d) % dimensions;
                var cmp = Key(a, axis, absolute).CompareTo(Key(b, axis, absolute));
                if (cmp != 0) return cmp;
            }
            return 0;
        });
    }

    //Key 排序键 取该维参数中心 需要绝对值时先取绝对值对应原版 RTree.comparator
    private static long Key(Node node, int dimension, bool absolute)
    {
        var span = node.Space[dimension];
        var centre = (span.Min + span.Max) / 2;
        return absolute ? Math.Abs(centre) : centre;
    }

    //Bucketize 按 6 的幂分组对应原版 RTree.bucketize
    private static List<SubTree> Bucketize(List<Node> nodes)
    {
        var buckets = new List<SubTree>();
        var expected = (int)Math.Pow(ChildrenPerNode,
            Math.Floor(Math.Log(nodes.Count - 0.01) / Math.Log(ChildrenPerNode)));
        if (expected < 1) expected = 1;

        var children = new List<Node>();
        foreach (var child in nodes)
        {
            children.Add(child);
            if (children.Count < expected) continue;
            buckets.Add(new SubTree(children));
            children = new List<Node>();
        }
        if (children.Count > 0) buckets.Add(new SubTree(children));
        return buckets;
    }

    //Cost 节点包围盒各维跨度之和 用于挑选分桶维度对应原版 RTree.cost
    private static long Cost(IReadOnlyList<Climate.Parameter> space)
    {
        var result = 0L;
        foreach (var span in space) result += Math.Abs(span.Max - span.Min);
        return result;
    }

    //Magnitude 参数中心绝对值之和 叶子数不多时用它排序对应原版 RTree.build 的排序键
    private static long Magnitude(Node node)
    {
        var total = 0L;
        foreach (var span in node.Space) total += Math.Abs((span.Min + span.Max) / 2);
        return total;
    }

    //BuildParameterSpace 逐维合并孩子包围盒对应原版 RTree.buildParameterSpace
    private static Climate.Parameter[] BuildParameterSpace(IReadOnlyList<Node> children)
    {
        if (children.Count == 0)
            throw new ArgumentException("SubTree needs at least one child");
        var bounds = new Climate.Parameter[ParameterDimensions];
        for (var d = 0; d < ParameterDimensions; d++)
        {
            var bound = children[0].Space[d];
            for (var i = 1; i < children.Count; i++)
                bound = bound.Merge(children[i].Space[d]);
            bounds[d] = bound;
        }
        return bounds;
    }
}
