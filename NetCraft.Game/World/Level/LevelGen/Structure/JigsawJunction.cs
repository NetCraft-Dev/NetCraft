using NetCraft.Codec;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//JigsawJunction jigsaw junction, maps to vanilla net.minecraft.world.level.levelgen.structure.pools.JigsawJunction
//Records the source position, relative height delta and dest projection of one jigsaw connection point; participates in terrain adaptation during jigsaw generation
public sealed class JigsawJunction
{
    //Codec junction codec, matches the vanilla serialize / deserialize field layout
    public static readonly Codec<JigsawJunction> Codec = new JigsawJunctionCodec();

    public JigsawJunction(int sourceX, int sourceGroundY, int sourceZ, int deltaY,
        StructureTemplatePool.Projection destProjection)
    {
        SourceX = sourceX;
        SourceGroundY = sourceGroundY;
        SourceZ = sourceZ;
        DeltaY = deltaY;
        DestProjection = destProjection;
    }

    public int SourceX { get; }

    public int SourceGroundY { get; }

    public int SourceZ { get; }

    public int DeltaY { get; }

    public StructureTemplatePool.Projection DestProjection { get; }

    //Equals vanilla comparison omits source ground Y; aligned here
    public override bool Equals(object? obj)
        => obj is JigsawJunction other
            && SourceX == other.SourceX
            && SourceZ == other.SourceZ
            && DeltaY == other.DeltaY
            && DestProjection == other.DestProjection;

    public override int GetHashCode() => HashCode.Combine(SourceX, SourceGroundY, SourceZ, DeltaY, DestProjection);

    public override string ToString()
        => $"JigsawJunction{{sourceX={SourceX}, sourceGroundY={SourceGroundY}, sourceZ={SourceZ}, deltaY={DeltaY}, destProjection={DestProjection}}}";
}

//JigsawJunctionCodec field codec for a junction: four ints plus a projection name
internal sealed class JigsawJunctionCodec : ScalarCodec<JigsawJunction>
{
    public override DataResult<JigsawJunction> Parse<U>(DynamicOps<U> ops, U input)
        => ops.GetMap(input).FlatMap(map => DecodeJunction(ops, map));

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, JigsawJunction value)
    {
        var builder = ops.MapBuilder();
        builder.Add("source_x", ops.CreateInt(value.SourceX));
        builder.Add("source_ground_y", ops.CreateInt(value.SourceGroundY));
        builder.Add("source_z", ops.CreateInt(value.SourceZ));
        builder.Add("delta_y", ops.CreateInt(value.DeltaY));
        builder.Add("dest_proj", ops.CreateString(PoolProjections.Name(value.DestProjection)));
        return builder.Build(ops.Empty());
    }

    private static DataResult<JigsawJunction> DecodeJunction<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        //Missing ints default to 0, matching the vanilla lenient asInt(0) read
        return ReadInt(ops, input, "source_x").FlatMap(sourceX =>
            ReadInt(ops, input, "source_ground_y").FlatMap(sourceGroundY =>
                ReadInt(ops, input, "source_z").FlatMap(sourceZ =>
                    ReadInt(ops, input, "delta_y").FlatMap(deltaY => ReadProjection(ops, input)
                        .Map(projection => new JigsawJunction(sourceX, sourceGroundY, sourceZ, deltaY, projection))))));
    }

    private static DataResult<int> ReadInt<U>(DynamicOps<U> ops, MapLike<U> input, string key)
    {
        var tag = input.Get(key);
        if (!tag.IsPresent) return DataResult<int>.Success(0);
        return ops.GetNumberValue(tag.Get()).Map(value => (int)value);
    }

    private static DataResult<StructureTemplatePool.Projection> ReadProjection<U>(DynamicOps<U> ops, MapLike<U> input)
    {
        var tag = input.Get("dest_proj");
        if (!tag.IsPresent) return DataResult<StructureTemplatePool.Projection>.Error(() => "junction is missing dest_proj");
        var text = ops.GetStringValue(tag.Get());
        if (!text.Result().IsPresent) return DataResult<StructureTemplatePool.Projection>.Error(() => "dest_proj must be a string");
        var parsed = PoolProjections.TryParse(text.GetOrThrow());
        return parsed is null
            ? DataResult<StructureTemplatePool.Projection>.Error(() => $"unknown dest_proj: {text.GetOrThrow()}")
            : DataResult<StructureTemplatePool.Projection>.Success(parsed.Value);
    }
}
