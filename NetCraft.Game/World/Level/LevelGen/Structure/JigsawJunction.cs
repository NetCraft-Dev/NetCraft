using NetCraft.Codec;

namespace NetCraft.Game.World.Level.LevelGen.Structure;

//JigsawJunction 拼图接缝 对应原版 net.minecraft.world.level.levelgen.structure.pools.JigsawJunction
//记录一个拼图连接点的来源坐标 相对高度差与目标投影 生成拼图时参与地形适配
public sealed class JigsawJunction
{
    //Codec 接缝编解码 对应原版 serialize / deserialize 的字段布局
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

    //Equals 原版比较里不含来源地面高度 这里对齐
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

//JigsawJunctionCodec 接缝的字段编解码 四个整数加一个投影名
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
        //四个整数缺省按 0 对应原版 asInt(0) 的宽松读法
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
        if (!tag.IsPresent) return DataResult<StructureTemplatePool.Projection>.Error(() => "接缝缺少 dest_proj");
        var text = ops.GetStringValue(tag.Get());
        if (!text.Result().IsPresent) return DataResult<StructureTemplatePool.Projection>.Error(() => "dest_proj 必须是字符串");
        var parsed = PoolProjections.TryParse(text.GetOrThrow());
        return parsed is null
            ? DataResult<StructureTemplatePool.Projection>.Error(() => $"未知的 dest_proj: {text.GetOrThrow()}")
            : DataResult<StructureTemplatePool.Projection>.Success(parsed.Value);
    }
}
