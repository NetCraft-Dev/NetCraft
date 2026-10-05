using NetCraft.Codec;
using NetCraft.Network;

namespace NetCraft.Game.World.Items.Component;

//FireworkExplosion 烟花爆炸效果 对应原版 net.minecraft.world.item.component.FireworkExplosion
//形状加主色加褪色加拖尾与闪烁四个要素
public sealed class FireworkExplosion : IEquatable<FireworkExplosion>
{
    //ShapeCodec 形状按名字编解码 对应原版 Shape.CODEC
    //必须先于 Codec 声明 静态字段按声明顺序初始化
    public static readonly Codec<ShapeKind> ShapeCodec = Codecs.String.ComapFlatMap(
        name =>
        {
            foreach (var shape in Enum.GetValues<ShapeKind>())
                if (string.Equals(shape.ToString(), name, StringComparison.OrdinalIgnoreCase))
                    return DataResult<ShapeKind>.Success(shape);
            return DataResult<ShapeKind>.Error(() => $"未知的烟花形状: {name}");
        },
        shape => shape.ToString().ToLowerInvariant());

    //Codec 持久化编解码 字段名 shape 与 colors 与 fade_colors 与 trail 与 twinkle 对应原版 CODEC
    public static readonly Codec<FireworkExplosion> Codec = RecordCodecBuilder.Of5(
        ShapeCodec.FieldOf("shape").ForGetter((FireworkExplosion explosion) => explosion.Shape),
        Codecs.Int.ListOf().OptionalFieldOf("colors", Array.Empty<int>())
            .ForGetter((FireworkExplosion explosion) => explosion.Colors),
        Codecs.Int.ListOf().OptionalFieldOf("fade_colors", Array.Empty<int>())
            .ForGetter((FireworkExplosion explosion) => explosion.FadeColors),
        Codecs.Bool.OptionalFieldOf("trail", false).ForGetter((FireworkExplosion explosion) => explosion.Trail),
        Codecs.Bool.OptionalFieldOf("twinkle", false).ForGetter((FireworkExplosion explosion) => explosion.Twinkle),
        (shape, colors, fadeColors, trail, twinkle)
            => new FireworkExplosion(shape, colors, fadeColors, trail, twinkle));

    //StreamCodec 网络编解码 形状写 id 颜色写变长整数列表 对应原版 STREAM_CODEC
    public static readonly StreamCodec<RegistryFriendlyByteBuf, FireworkExplosion> StreamCodec =
        new FireworkExplosionStreamCodec();

    public FireworkExplosion(
        ShapeKind shape, IReadOnlyList<int> colors, IReadOnlyList<int> fadeColors, bool trail, bool twinkle)
    {
        Shape = shape;
        Colors = colors;
        FadeColors = fadeColors;
        Trail = trail;
        Twinkle = twinkle;
    }

    public ShapeKind Shape { get; }
    public IReadOnlyList<int> Colors { get; }
    public IReadOnlyList<int> FadeColors { get; }
    public bool Trail { get; }
    public bool Twinkle { get; }

    //HasShape 非小型视为有形状 对应原版 hasShape
    public bool HasShape() => Shape != ShapeKind.Small;

    //ById 按网络 id 取形状
    public static ShapeKind ById(int id) => (ShapeKind)id;

    //ShapeKind 烟花形状 值即网络 id 对应原版 Shape
    public enum ShapeKind
    {
        Small,
        Large,
        Star,
        Burst,
        Creeper
    }

    //判等按内容 颜色列表逐项比较
    public bool Equals(FireworkExplosion? other)
        => other is not null
           && Shape == other.Shape
           && Trail == other.Trail
           && Twinkle == other.Twinkle
           && Colors.SequenceEqual(other.Colors)
           && FadeColors.SequenceEqual(other.FadeColors);

    public override bool Equals(object? obj) => Equals(obj as FireworkExplosion);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Shape);
        hash.Add(Trail);
        hash.Add(Twinkle);
        foreach (var color in Colors) hash.Add(color);
        foreach (var color in FadeColors) hash.Add(color);
        return hash.ToHashCode();
    }

    public override string ToString()
        => $"FireworkExplosion[{Shape}, colors={Colors.Count}, fadeColors={FadeColors.Count}, trail={Trail}, twinkle={Twinkle}]";
}

//FireworkExplosionStreamCodec 形状 id 加两个颜色列表加两个布尔 对应原版 STREAM_CODEC
internal sealed class FireworkExplosionStreamCodec : StreamCodec<RegistryFriendlyByteBuf, FireworkExplosion>
{
    public FireworkExplosion Decode(RegistryFriendlyByteBuf buf)
    {
        var shape = FireworkExplosion.ById(buf.ReadByte());
        var colors = ReadIntList(buf);
        var fadeColors = ReadIntList(buf);
        var trail = buf.ReadBoolean();
        var twinkle = buf.ReadBoolean();
        return new FireworkExplosion(shape, colors, fadeColors, trail, twinkle);
    }

    public void Encode(RegistryFriendlyByteBuf buf, FireworkExplosion value)
    {
        buf.WriteByte((byte)value.Shape);
        WriteIntList(buf, value.Colors);
        WriteIntList(buf, value.FadeColors);
        buf.WriteBoolean(value.Trail);
        buf.WriteBoolean(value.Twinkle);
    }

    //ReadIntList 变长整数长度前缀再逐个变长整数
    private static IReadOnlyList<int> ReadIntList(RegistryFriendlyByteBuf buf)
    {
        var size = buf.ReadVarInt();
        var list = new List<int>(Math.Min(size, ByteBufCodecs.MaxInitialCollectionSize));
        for (var i = 0; i < size; i++) list.Add(buf.ReadVarInt());
        return list;
    }

    private static void WriteIntList(RegistryFriendlyByteBuf buf, IReadOnlyList<int> values)
    {
        buf.WriteVarInt(values.Count);
        foreach (var value in values) buf.WriteVarInt(value);
    }
}
