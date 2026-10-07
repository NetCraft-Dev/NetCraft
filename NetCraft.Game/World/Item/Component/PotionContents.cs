using NetCraft.Codec;
using NetCraft.Game.World.Effect;
using NetCraft.Network;
using NetCraft.Registry;

namespace NetCraft.Game.World.Items.Component;

//PotionContents potion contents: base potion plus custom color plus custom effects plus custom name
//Maps to vanilla net.minecraft.world.item.component.PotionContents
public sealed class PotionContents : IEquatable<PotionContents>
{
    //Codec persistence codec, all four fields are optional, maps to vanilla CODEC
    public static readonly Codec<PotionContents> Codec = RecordCodecBuilder.Of4(
        HolderSetCodecs.PotionRef.OptionalFieldOf("potion").ForGetter((PotionContents contents) => contents.Potion),
        Codecs.Int.OptionalFieldOf("custom_color").ForGetter((PotionContents contents) => contents.CustomColor),
        MobEffectInstance.Codec.ListOf().OptionalFieldOf("custom_effects", Array.Empty<MobEffectInstance>())
            .ForGetter((PotionContents contents) => contents.CustomEffects),
        Codecs.String.OptionalFieldOf("custom_name").ForGetter((PotionContents contents) => contents.CustomName),
        (potion, customColor, customEffects, customName)
            => new PotionContents(potion, customColor, customEffects, customName));

    //StreamCodec network codec, each field is optional, maps to vanilla STREAM_CODEC
    public static readonly StreamCodec<RegistryFriendlyByteBuf, PotionContents> StreamCodec =
        new PotionContentsStreamCodec();

    //Empty empty potion contents, maps to vanilla EMPTY
    public static readonly PotionContents Empty = new(
        Optional<Holder<Potion>>.Empty(), Optional<int>.Empty(),
        Array.Empty<MobEffectInstance>(), Optional<string>.Empty());

    public PotionContents(
        Optional<Holder<Potion>> potion, Optional<int> customColor,
        IReadOnlyList<MobEffectInstance> customEffects, Optional<string> customName)
    {
        Potion = potion;
        CustomColor = customColor;
        CustomEffects = customEffects;
        CustomName = customName;
    }

    public Optional<Holder<Potion>> Potion { get; }
    public Optional<int> CustomColor { get; }
    public IReadOnlyList<MobEffectInstance> CustomEffects { get; }
    public Optional<string> CustomName { get; }

    public bool Equals(PotionContents? other)
        => other is not null
           && Potion.IsPresent == other.Potion.IsPresent
           && (!Potion.IsPresent || ReferenceEquals(Potion.Get(), other.Potion.Get()))
           && CustomColor.IsPresent == other.CustomColor.IsPresent
           && (!CustomColor.IsPresent || CustomColor.Get() == other.CustomColor.Get())
           && CustomEffects.SequenceEqual(other.CustomEffects)
           && CustomName.OrElse(string.Empty) == other.CustomName.OrElse(string.Empty);

    public override bool Equals(object? obj) => Equals(obj as PotionContents);

    public override int GetHashCode() => HashCode.Combine(Potion.IsPresent, CustomColor.IsPresent, CustomEffects.Count);

    public override string ToString() => $"PotionContents[effects={CustomEffects.Count}]";
}

//PotionContentsStreamCodec optional potion plus optional color plus effect list plus optional name, maps to vanilla STREAM_CODEC
internal sealed class PotionContentsStreamCodec : StreamCodec<RegistryFriendlyByteBuf, PotionContents>
{
    private static readonly StreamCodec<RegistryFriendlyByteBuf, Holder<Potion>> PotionStream =
        ByteBufCodecs.Holder(Registries.POTION);

    public PotionContents Decode(RegistryFriendlyByteBuf buf)
    {
        var potion = buf.ReadBoolean()
            ? Optional<Holder<Potion>>.Of(PotionStream.Decode(buf))
            : Optional<Holder<Potion>>.Empty();
        var customColor = buf.ReadBoolean() ? Optional<int>.Of(buf.ReadInt()) : Optional<int>.Empty();
        var size = buf.ReadVarInt();
        var customEffects = new List<MobEffectInstance>(Math.Min(size, ByteBufCodecs.MaxInitialCollectionSize));
        for (var i = 0; i < size; i++) customEffects.Add(MobEffectInstance.StreamCodec.Decode(buf));
        var customName = buf.ReadBoolean() ? Optional<string>.Of(buf.ReadString()) : Optional<string>.Empty();
        return new PotionContents(potion, customColor, customEffects, customName);
    }

    public void Encode(RegistryFriendlyByteBuf buf, PotionContents value)
    {
        buf.WriteBoolean(value.Potion.IsPresent);
        if (value.Potion.IsPresent) PotionStream.Encode(buf, value.Potion.Get());
        buf.WriteBoolean(value.CustomColor.IsPresent);
        if (value.CustomColor.IsPresent) buf.WriteInt(value.CustomColor.Get());
        buf.WriteVarInt(value.CustomEffects.Count);
        foreach (var effect in value.CustomEffects) MobEffectInstance.StreamCodec.Encode(buf, effect);
        buf.WriteBoolean(value.CustomName.IsPresent);
        if (value.CustomName.IsPresent) buf.WriteString(value.CustomName.Get());
    }
}
