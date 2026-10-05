using NetCraft.Codec;
using NetCraft.Network;
using NetCraft.Registry;

namespace NetCraft.Game.World.Items.Component;

//ArmorTrim 盔甲纹饰 材料引用加图案引用 对应原版 net.minecraft.world.item.equipment.trim.ArmorTrim
public sealed class ArmorTrim : IEquatable<ArmorTrim>
{
    //Codec 持久化编解码 两个引用都是必填 对应原版 CODEC
    public static readonly Codec<ArmorTrim> Codec = RecordCodecBuilder.Of2(
        HolderSetCodecs.TrimMaterialRef.FieldOf("material").ForGetter((ArmorTrim trim) => trim.Material),
        HolderSetCodecs.TrimPatternRef.FieldOf("pattern").ForGetter((ArmorTrim trim) => trim.Pattern),
        (material, pattern) => new ArmorTrim(material, pattern));

    //StreamCodec 网络编解码 两个注册表引用按 id 进出 对应原版 STREAM_CODEC
    public static readonly StreamCodec<RegistryFriendlyByteBuf, ArmorTrim> StreamCodec = new ArmorTrimStreamCodec();

    public ArmorTrim(Holder<TrimMaterial> material, Holder<TrimPattern> pattern)
    {
        Material = material;
        Pattern = pattern;
    }

    public Holder<TrimMaterial> Material { get; }
    public Holder<TrimPattern> Pattern { get; }

    public bool Equals(ArmorTrim? other)
        => other is not null && Equals(Material, other.Material) && Equals(Pattern, other.Pattern);

    public override bool Equals(object? obj) => Equals(obj as ArmorTrim);

    public override int GetHashCode() => HashCode.Combine(Material, Pattern);

    public override string ToString() => $"ArmorTrim[{Material}, {Pattern}]";
}

//ArmorTrimStreamCodec 材料引用加图案引用 对应原版 STREAM_CODEC
internal sealed class ArmorTrimStreamCodec : StreamCodec<RegistryFriendlyByteBuf, ArmorTrim>
{
    private static readonly StreamCodec<RegistryFriendlyByteBuf, Holder<TrimMaterial>> MaterialCodec =
        ByteBufCodecs.Holder(Registries.TRIM_MATERIAL);

    private static readonly StreamCodec<RegistryFriendlyByteBuf, Holder<TrimPattern>> PatternCodec =
        ByteBufCodecs.Holder(Registries.TRIM_PATTERN);

    public ArmorTrim Decode(RegistryFriendlyByteBuf buf)
        => new(MaterialCodec.Decode(buf), PatternCodec.Decode(buf));

    public void Encode(RegistryFriendlyByteBuf buf, ArmorTrim value)
    {
        MaterialCodec.Encode(buf, value.Material);
        PatternCodec.Encode(buf, value.Pattern);
    }
}
