namespace NetCraft.Game.World.Level.LevelGen;

//ClimateRTree nearest-neighbour search tree over climate parameter space, maps to vanilla Climate.RTree
//The parameter list has thousands of entries and a linear scan would walk the whole list on every sample; the tree only descends a few subtrees
//Each internal node has at most 6 children; the tree picks the dimension with the smallest total bounding box to bucket, and the query prunes by child bounding-box distance
internal sealed class ClimateRTree<T>
{
    //ChildrenPerNode children per internal node, maps to vanilla CHILDREN_PER_NODE
    private const int ChildrenPerNode = 6;

    //ParameterDimensions number of parameter-space dimensions, the 6 climate dimensions plus offset
    private const int ParameterDimensions = 7;

    private readonly Node _root;

    //The previous query result seeds the next candidate; neighbouring samples have similar climates so the hit rate is high and saves a lot of subtree traversal
    //Must be per thread: the tree is shared globally, and concurrent generation queries sharing one would overwrite each other
    //Once the candidate is clobbered the pruning start lands near another thread's target, almost no subtree is pruned and a search goes from microseconds to tens of microseconds
    //Matches vanilla RTree's ThreadLocal lastResult
    private readonly ThreadLocal<Leaf?> _lastResult = new();

    private ClimateRTree(Node root) => _root = root;

    //Create builds the tree from parameter list entries, maps to vanilla RTree.create
    public static ClimateRTree<T> Create(IReadOnlyList<(Climate.ParameterPoint Point, T Value)> values)
    {
        if (values.Count == 0)
            throw new ArgumentException("Need at least one value to build the search tree.");
        var leaves = new List<Node>(values.Count);
        foreach (var (point, value) in values)
            leaves.Add(new Leaf(point, value));
        return new ClimateRTree<T>(Build(ParameterDimensions, leaves));
    }

    //Search looks up the nearest neighbour, maps to vanilla RTree.search
    public T Search(Climate.ParameterPoint target)
    {
        var leaf = _root.Search(new ParameterTarget(target), _lastResult.Value);
        _lastResult.Value = leaf;
        return leaf.Value;
    }

    //ParameterTarget 7-dimensional query value, maps to the long[] in vanilla RTree.search
    //A value type so it stays on the stack; vanilla relies on Java escape analysis to elide the array
    //In .NET it is passed to the virtual Search and escape analysis cannot help, so it is explicitly a value type
    private readonly struct ParameterTarget
    {
        public readonly long Temperature;
        public readonly long Humidity;
        public readonly long Continentalness;
        public readonly long Erosion;
        public readonly long Depth;
        public readonly long Weirdness;
        public readonly long Offset;

        //Every dimension of the target point is a single value, so Min suffices
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

    //SpaceOf expands a parameter point into a 7-dimensional bounding box, treating offset as a single-value range
    private static Climate.Parameter[] SpaceOf(Climate.ParameterPoint point)
        => new[]
        {
            point.Temperature, point.Humidity, point.Continentalness,
            point.Erosion, point.Depth, point.Weirdness, Climate.Parameter.Single(point.Offset)
        };

    //Node search tree node holding a 7-dimensional bounding box, maps to vanilla RTree.Node
    private abstract class Node
    {
        //Space is a field rather than a property; the hot path reads it on every Distance computation and property calls show up in sampling
        public readonly Climate.Parameter[] Space;

        protected Node(IReadOnlyList<Climate.Parameter> space) => Space = space.ToArray();

        //Distance distance from the node's bounding box to the target, per-dimension range distance then sum of squares
        //Unrolled into 7 expressions rather than a loop; the loop exceeds the JIT inline budget and would cost one call per node if not inlined
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

        //Delta2 squared single-dimension range distance; a value inside the range contributes 0, maps to vanilla Parameter.distance
        private static long Delta2(long target, in Climate.Parameter span)
        {
            var above = target - span.Max;
            if (above > 0) return above * above;
            var below = span.Min - target;
            return below > 0 ? below * below : 0L;
        }

        public abstract Leaf Search(in ParameterTarget target, Leaf? candidate);
    }

    //Leaf leaf node holding one parameter list entry, maps to vanilla RTree.Leaf
    private sealed class Leaf : Node
    {
        public T Value { get; }

        public Leaf(Climate.ParameterPoint point, T value) : base(SpaceOf(point)) => Value = value;

        public override Leaf Search(in ParameterTarget target, Leaf? candidate) => this;
    }

    //SubTree internal node holding several children, maps to vanilla RTree.SubTree
    private sealed class SubTree : Node
    {
        //Children child array traversed on every query; storing an array takes the indexed path instead of a boxed interface enumerator
        public Node[] Children { get; }

        public SubTree(IReadOnlyList<Node> children)
            : this(BuildParameterSpace(children), children)
        {
        }

        public SubTree(IReadOnlyList<Climate.Parameter> space, IReadOnlyList<Node> children)
            : base(space) => Children = children as Node[] ?? children.ToArray();

        //Search computes the child bounding-box distance first and skips a whole child that is farther than the current best
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

    //Build recursively builds the tree, maps to vanilla RTree.build
    //With child count within one page it sorts by the sum of parameter-centre magnitudes; otherwise it tries bucketing per dimension and takes the one with the smallest total bounding box
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

    //Sort compares by the given dimension first then rotates from it, maps to vanilla RTree.sort
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

    //Key sort key, the parameter centre of that dimension, taking the absolute value first when needed, maps to vanilla RTree.comparator
    private static long Key(Node node, int dimension, bool absolute)
    {
        var span = node.Space[dimension];
        var centre = (span.Min + span.Max) / 2;
        return absolute ? Math.Abs(centre) : centre;
    }

    //Bucketize groups by powers of 6, maps to vanilla RTree.bucketize
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

    //Cost sum of the per-dimension spans of a node's bounding box, used to pick the bucketing dimension, maps to vanilla RTree.cost
    private static long Cost(IReadOnlyList<Climate.Parameter> space)
    {
        var result = 0L;
        foreach (var span in space) result += Math.Abs(span.Max - span.Min);
        return result;
    }

    //Magnitude sum of absolute parameter centres, the sort key when there are few leaves, maps to the sort key of vanilla RTree.build
    private static long Magnitude(Node node)
    {
        var total = 0L;
        foreach (var span in node.Space) total += Math.Abs((span.Min + span.Max) / 2);
        return total;
    }

    //BuildParameterSpace merges the child bounding boxes per dimension, maps to vanilla RTree.buildParameterSpace
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
