using NetCraft.Codec;
using NetCraft.Network;

namespace NetCraft.Game.World.Items.Component;

//FireworkExplosion firework explosion effect, maps to vanilla net.minecraft.world.item.component.FireworkExplosion
//Four elements: shape, colors, fade colors, and trail plus twinkle
public sealed class FireworkExplosion : IEquatable<FireworkExplosion>
{
    //ShapeCodec encodes the shape by name, maps to vanilla Shape.CODEC
    //Must be declared before Codec, static fields initialize in declaration order
    public static readonly Codec<ShapeKind> ShapeCodec = Codecs.String.ComapFlatMap(
        name =>
        {
            foreach (var shape in Enum.GetValues<ShapeKind>())
                if (string.Equals(shape.ToString(), name, StringComparison.OrdinalIgnoreCase))
                    return DataResult<ShapeKind>.Success(shape);
            return DataResult<ShapeKind>.Error(() => $"unknown firework shape: {name}");
        },
        shape => shape.ToString().ToLowerInvariant());

    //Codec persistence codec, field names shape, colors, fade_colors, trail and twinkle, maps to vanilla CODEC
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

    //StreamCodec network codec, the shape writes as an id and colors as a VarInt list, maps to vanilla STREAM_CODEC
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

    //HasShape anything other than small counts as having a shape, maps to vanilla hasShape
    public bool HasShape() => Shape != ShapeKind.Small;

    //ById resolves a shape by network id
    public static ShapeKind ById(int id) => (ShapeKind)id;

    //ShapeKind firework shape, the value is the network id, maps to vanilla Shape
    public enum ShapeKind
    {
        Small,
        Large,
        Star,
        Burst,
        Creeper
    }

    //Equality compares by content, the color lists are compared entry by entry
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

//FireworkExplosionStreamCodec shape id plus two color lists plus two booleans, maps to vanilla STREAM_CODEC
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

    //ReadIntList a VarInt length prefix followed by each VarInt
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
