using NetCraft.Codec;
using NetCraft.Network;

namespace NetCraft.Game.World.Items.Component;

//Fireworks 烟花火箭数据 飞行时长加爆炸效果列表 对应原版 net.minecraft.world.item.component.Fireworks
public sealed class Fireworks : IEquatable<Fireworks>
{
    //Codec 持久化编解码 两个字段都是必填 对应原版 CODEC
    public static readonly Codec<Fireworks> Codec = RecordCodecBuilder.Of2(
        Codecs.Int.FieldOf("flight_duration").ForGetter((Fireworks fireworks) => fireworks.FlightDuration),
        FireworkExplosion.Codec.ListOf().FieldOf("explosions")
            .ForGetter((Fireworks fireworks) => fireworks.Explosions),
        (flightDuration, explosions) => new Fireworks(flightDuration, explosions));

    //StreamCodec 网络编解码 对应原版 STREAM_CODEC
    public static readonly StreamCodec<RegistryFriendlyByteBuf, Fireworks> StreamCodec = new FireworksStreamCodec();

    //Default 默认烟花 飞行时长 1 无爆炸 对应原版 DEFAULT
    public static readonly Fireworks Default = new(1, Array.Empty<FireworkExplosion>());

    public Fireworks(int flightDuration, IReadOnlyList<FireworkExplosion> explosions)
    {
        FlightDuration = flightDuration;
        Explosions = explosions;
    }

    public int FlightDuration { get; }
    public IReadOnlyList<FireworkExplosion> Explosions { get; }

    //判等按内容 爆炸列表逐项比较
    public bool Equals(Fireworks? other)
        => other is not null
           && FlightDuration == other.FlightDuration
           && Explosions.SequenceEqual(other.Explosions);

    public override bool Equals(object? obj) => Equals(obj as Fireworks);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(FlightDuration);
        foreach (var explosion in Explosions) hash.Add(explosion);
        return hash.ToHashCode();
    }

    public override string ToString() => $"Fireworks[{FlightDuration}, explosions={Explosions.Count}]";
}

//FireworksStreamCodec 飞行时长加爆炸列表 对应原版 STREAM_CODEC
internal sealed class FireworksStreamCodec : StreamCodec<RegistryFriendlyByteBuf, Fireworks>
{
    public Fireworks Decode(RegistryFriendlyByteBuf buf)
    {
        var flightDuration = buf.ReadVarInt();
        var size = buf.ReadVarInt();
        var explosions = new List<FireworkExplosion>(Math.Min(size, ByteBufCodecs.MaxInitialCollectionSize));
        for (var i = 0; i < size; i++) explosions.Add(FireworkExplosion.StreamCodec.Decode(buf));
        return new Fireworks(flightDuration, explosions);
    }

    public void Encode(RegistryFriendlyByteBuf buf, Fireworks value)
    {
        buf.WriteVarInt(value.FlightDuration);
        buf.WriteVarInt(value.Explosions.Count);
        foreach (var explosion in value.Explosions) FireworkExplosion.StreamCodec.Encode(buf, explosion);
    }
}
