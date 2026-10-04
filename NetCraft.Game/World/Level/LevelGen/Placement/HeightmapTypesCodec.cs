using NetCraft.Codec;
using NetCraft.Registry;

namespace NetCraft.Game.World.Level.LevelGen.Placement;

//HeightmapTypesCodec 高度图类型编解码对应原版 Heightmap.Types.CODEC
//JSON 形态是大写下划线的序列化键 如 WORLD_SURFACE_WG
internal sealed class HeightmapTypesCodec : ScalarCodec<Heightmap.Types>
{
    public static readonly HeightmapTypesCodec Instance = new();

    public override DataResult<Heightmap.Types> Parse<U>(DynamicOps<U> ops, U input)
    {
        var text = ops.GetStringValue(input);
        if (!text.Result().IsPresent)
            return DataResult<Heightmap.Types>.Error(() => "高度图类型必须是字符串");
        var type = Heightmap.FromSerializationKey(text.GetOrThrow());
        return type is null
            ? DataResult<Heightmap.Types>.Error(() => $"未知的高度图类型: {text.GetOrThrow()}")
            : DataResult<Heightmap.Types>.Success(type.Value);
    }

    public override DataResult<U> EncodeStart<U>(DynamicOps<U> ops, Heightmap.Types value)
        => DataResult<U>.Success(ops.CreateString(value.GetSerializationKey()));
}
