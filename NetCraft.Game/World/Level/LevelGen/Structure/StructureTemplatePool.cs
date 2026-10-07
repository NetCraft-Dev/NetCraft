using NetCraft.Codec;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Util.Collection;
using NetCraft.Util.Random;
using HeightmapTypes = NetCraft.Registry.Heightmap.Types;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//StructureTemplatePool template pool, maps to vanilla net.minecraft.world.level.levelgen.structure.pools.StructureTemplatePool
//A pool = a set of weighted pool elements + a fallback pool; jigsaw connection expands by weight then draws one element at random
public sealed class StructureTemplatePool : NetCraft.Registry.StructureTemplatePool
{
    //SizeUnset sentinel before the max size is computed, maps to vanilla SIZE_UNSET
    private const int SizeUnset = int.MinValue;

    private static Codec<StructureTemplatePool>? _directCodec;

    //DirectCodec the pool's own codec, used when a pool reference is defined inline; built lazily to avoid entangling init with the reference codec
    public static Codec<StructureTemplatePool> DirectCodec => _directCodec ??= new StructureTemplatePoolCodec();

    //ElementCodec registry element codec; the registry holds elements through the marker interface
    public static readonly Codec<NetCraft.Registry.StructureTemplatePool> ElementCodec = DirectCodec.ComapFlatMap(
        pool => DataResult<NetCraft.Registry.StructureTemplatePool>.Success(pool),
        pool => (StructureTemplatePool)pool);

    private readonly List<StructurePoolElementEntry> _rawTemplates;
    private readonly List<StructurePoolElement> _templates;
    private int _maxSize = SizeUnset;

    public StructureTemplatePool(Holder<NetCraft.Registry.StructureTemplatePool> fallback,
        IReadOnlyList<StructurePoolElementEntry> templates)
    {
        _rawTemplates = new List<StructurePoolElementEntry>(templates);
        _templates = new List<StructurePoolElement>();
        //Expands each element into the template list by weight, entering once per weight
        foreach (var entry in templates)
        {
            for (var i = 0; i < entry.Weight; i++) _templates.Add(entry.Element);
        }
        Fallback = fallback;
    }

    //Fallback the fallback pool used when nothing can be drawn from this pool
    public Holder<NetCraft.Registry.StructureTemplatePool> Fallback { get; }

    //RawTemplates element definitions before weight expansion
    public IReadOnlyList<StructurePoolElementEntry> RawTemplates => _rawTemplates;

    //GetMaxSize tallest element height in the pool, maps to vanilla getMaxSize; the result is cached, an empty pool yields 0
    public int GetMaxSize(StructureTemplateManager manager)
    {
        if (_maxSize != SizeUnset) return _maxSize;
        var maxSize = 0;
        foreach (var template in _templates)
        {
            if (ReferenceEquals(template, EmptyPoolElement.Instance)) continue;
            var span = template.GetBoundingBox(manager, BlockPos.Zero, Rotation.None).LengthY;
            if (span > maxSize) maxSize = span;
        }
        _maxSize = maxSize;
        return _maxSize;
    }

    //GetRandomTemplate draws one at random from the weighted list, an empty pool yields the empty element, maps to vanilla getRandomTemplate
    public StructurePoolElement GetRandomTemplate(RandomSource random)
        => _templates.Count == 0 ? EmptyPoolElement.Instance : _templates[random.NextInt(_templates.Count)];

    //GetShuffledTemplates shuffled copy of the weighted list, maps to vanilla getShuffledTemplates
    public List<StructurePoolElement> GetShuffledTemplates(RandomSource random)
        => RandomCollections.ShuffledCopy(_templates, random);

    //Size element count after weight expansion, maps to vanilla size
    public int Size() => _templates.Count;

    public override string ToString() => $"StructureTemplatePool[{_templates.Count} entries, fallback {Fallback.UnwrapKey()?.Identifier}]";

    //Projection projection; decides whether the structure snaps to the ground on placement, maps to vanilla StructureTemplatePool.Projection
    public enum Projection
    {
        TerrainMatching,
        Rigid,
    }
}

//StructurePoolElementEntry one element definition in a pool, maps to vanilla Pair<StructurePoolElement, Integer>
//weight is a positive integer weight, limited to 1..150 in vanilla
public sealed record StructurePoolElementEntry(StructurePoolElement Element, int Weight);

//PoolProjections projection name mapping and built-in processors, maps to the two enum constants of vanilla Projection
internal static class PoolProjections
{
    //The terrain_matching projection snaps the structure to the ground and carries a gravity processor that lowers it by 1 block
    private static readonly IReadOnlyList<StructureProcessor> TerrainMatchingProcessors =
        new StructureProcessor[] { new GravityProcessor(HeightmapTypes.WorldSurfaceWg, -1) };

    private static readonly IReadOnlyList<StructureProcessor> RigidProcessors = Array.Empty<StructureProcessor>();

    //Name returns the JSON name, maps to vanilla getSerializedName
    public static string Name(StructureTemplatePool.Projection projection)
        => projection == StructureTemplatePool.Projection.TerrainMatching ? "terrain_matching" : "rigid";

    //TryParse parses by JSON name, returns null when invalid
    public static StructureTemplatePool.Projection? TryParse(string name) => name switch
    {
        "terrain_matching" => StructureTemplatePool.Projection.TerrainMatching,
        "rigid" => StructureTemplatePool.Projection.Rigid,
        _ => null,
    };

    //GetProcessors the processors built into this projection, maps to vanilla Projection.getProcessors
    public static IReadOnlyList<StructureProcessor> GetProcessors(StructureTemplatePool.Projection projection)
        => projection == StructureTemplatePool.Projection.TerrainMatching ? TerrainMatchingProcessors : RigidProcessors;
}

//StructureTemplatePoolCodec template pool codec, decodes fallback and the weighted list, maps to vanilla DIRECT_CODEC
internal sealed class StructureTemplatePoolCodec : ScalarCodec<StructureTemplatePool>
{
    //WeightMin/WeightMax allowed range for weight, maps to vanilla Codec.intRange(1, 150)
    private const int WeightMin = 1;
    private const int WeightMax = 150;

    public override DataResult<StructureTemplatePool> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map => DecodePool(ops, map));

    private static DataResult<StructureTemplatePool> DecodePool<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var fallbackTag = input.Get("fallback");
        if (!fallbackTag.IsPresent) return DataResult<StructureTemplatePool>.Error(() => "template pool is missing fallback");
        var fallback = StructurePoolCodecs.TemplatePoolRef.Parse(ops, fallbackTag.Get());
        if (!fallback.Result().IsPresent) return DataResult<StructureTemplatePool>.Error(() => "failed to parse template pool fallback");

        var elementsTag = input.Get("elements");
        if (!elementsTag.IsPresent) return DataResult<StructureTemplatePool>.Error(() => "template pool is missing elements");
        var stream = ops.GetStream(elementsTag.Get());
        if (!stream.Result().IsPresent) return DataResult<StructureTemplatePool>.Error(() => "elements must be an array");

        var entries = new List<StructurePoolElementEntry>();
        foreach (var item in stream.GetOrThrow())
        {
            var itemMap = ops.GetMap(item);
            if (!itemMap.Result().IsPresent) return DataResult<StructureTemplatePool>.Error(() => "elements entries must be objects");
            var map = itemMap.GetOrThrow();

            var elementTag = map.Get("element");
            if (!elementTag.IsPresent) return DataResult<StructureTemplatePool>.Error(() => "elements entry is missing element");
            var elementError = string.Empty;
            var element = StructurePoolElement.Codec.Parse(ops, elementTag.Get());
            var elementValue = element.ResultOrPartial(message => elementError = message);
            if (!elementValue.IsPresent)
                return DataResult<StructureTemplatePool>.Error(() => $"failed to parse elements element {elementError}");

            var weight = StructurePlacementCodecs.ReadIntField(ops, map, "weight");
            if (!weight.Result().IsPresent) return DataResult<StructureTemplatePool>.Error(() => "elements entry is missing weight");
            var value = weight.GetOrThrow();
            if (value < WeightMin || value > WeightMax)
                return DataResult<StructureTemplatePool>.Error(() => $"weight out of range, must be between {WeightMin}..{WeightMax}, got {value}");

            entries.Add(new StructurePoolElementEntry(elementValue.Get(), value));
        }

        return DataResult<StructureTemplatePool>.Success(new StructureTemplatePool(fallback.GetOrThrow(), entries));
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, StructureTemplatePool value)
        => DataResult<U>.Error(() => "template pool encoding not implemented yet");
}
