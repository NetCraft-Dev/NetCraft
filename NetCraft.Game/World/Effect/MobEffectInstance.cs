using NetCraft.Codec;
using NetCraft.Network;
using NetCraft.Registry;

namespace NetCraft.Game.World.Effect;

//MobEffectInstance mob effect instance, maps to vanilla net.minecraft.world.effect.MobEffectInstance
//An instance describes a mob effect's remaining duration and level on an entity; the player side stores it in the effect table by effect value
public sealed class MobEffectInstance
{
    //Codec persistence codec, effect reference plus duration and level and two display toggles, maps to vanilla CODEC
    public static readonly Codec<MobEffectInstance> Codec = RecordCodecBuilder.Of5(
        HolderSetCodecs.MobEffectRef.FieldOf("id").ForGetter((MobEffectInstance instance) => instance.Effect),
        Codecs.Int.OptionalFieldOf("duration", 0).ForGetter((MobEffectInstance instance) => instance.Duration),
        Codecs.Int.OptionalFieldOf("amplifier", 0).ForGetter((MobEffectInstance instance) => instance.Amplifier),
        Codecs.Bool.OptionalFieldOf("ambient", false).ForGetter((MobEffectInstance instance) => instance.IsAmbient),
        Codecs.Bool.OptionalFieldOf("show_particles", true).ForGetter((MobEffectInstance instance) => instance.IsVisible),
        (effect, duration, amplifier, ambient, visible)
            => new MobEffectInstance(effect, duration, amplifier, ambient, visible));

    //StreamCodec network codec, effect id plus duration and level and flags, maps to vanilla STREAM_CODEC
    public static readonly StreamCodec<RegistryFriendlyByteBuf, MobEffectInstance> StreamCodec =
        new MobEffectInstanceStreamCodec();

    public MobEffectInstance(Holder<NetCraft.Registry.MobEffect> effect, int duration, int amplifier, bool ambient, bool visible)
    {
        Effect = effect;
        Duration = duration;
        Amplifier = amplifier;
        IsAmbient = ambient;
        IsVisible = visible;
        //showIcon takes visible like vanilla
        ShowIcon = visible;
    }

    //Effect effect registry reference; the network codec uses it to look up the registry id
    public Holder<NetCraft.Registry.MobEffect> Effect { get; }

    //Duration remaining ticks; -1 means infinite
    public int Duration { get; private set; }

    //Amplifier effect level; 0 corresponds to level I
    public int Amplifier { get; }

    //IsAmbient whether it is an ambient effect; effects applied by beacons/conduits
    public bool IsAmbient { get; }

    //IsVisible whether particles are shown
    public bool IsVisible { get; }

    //ShowIcon whether the icon is shown
    public bool ShowIcon { get; }

    //Tick decrements the duration every tick; -1 means infinite and does not decrement
    public void Tick()
    {
        if (Duration > 0) Duration--;
    }

    //Expired duration reached zero, maps to the vanilla effect expiring
    public bool Expired => Duration == 0;
}

//MobEffectInstanceStreamCodec effect id plus duration and level and flags, maps to vanilla STREAM_CODEC
internal sealed class MobEffectInstanceStreamCodec : StreamCodec<RegistryFriendlyByteBuf, MobEffectInstance>
{
    public MobEffectInstance Decode(RegistryFriendlyByteBuf buf)
    {
        var effectId = buf.ReadVarInt();
        var effect = BuiltInRegistries.MOB_EFFECT.Get(effectId)
            ?? throw new InvalidOperationException($"unknown mob effect id {effectId}");
        var duration = buf.ReadVarInt();
        var amplifier = buf.ReadVarInt();
        var flags = buf.ReadByte();
        var ambient = (flags & 1) != 0;
        var visible = (flags & 2) != 0;
        return new MobEffectInstance(effect, duration, amplifier, ambient, visible);
    }

    public void Encode(RegistryFriendlyByteBuf buf, MobEffectInstance value)
    {
        var id = BuiltInRegistries.MOB_EFFECT.GetId(value.Effect.Value);
        if (id < 0) throw new InvalidOperationException($"mob effect not registered: {value.Effect.Value}");
        buf.WriteVarInt(id);
        buf.WriteVarInt(value.Duration);
        buf.WriteVarInt(value.Amplifier);
        byte flags = 0;
        if (value.IsAmbient) flags |= 1;
        if (value.IsVisible) flags |= 2;
        if (value.ShowIcon) flags |= 4;
        buf.WriteByte(flags);
    }
}
