using NetCraft.Codec;
using NetCraft.Network;
using NetCraft.Registry;

namespace NetCraft.Game.World.Effect;

//MobEffectInstance 药水效果实例 对应原版 net.minecraft.world.effect.MobEffectInstance
//一个实例描述某条效果在实体身上的剩余时长与等级 玩家侧按效果值存进效果表
public sealed class MobEffectInstance
{
    //Codec 持久化编解码 效果引用加时长与等级与两个显示开关 对应原版 CODEC
    public static readonly Codec<MobEffectInstance> Codec = RecordCodecBuilder.Of5(
        HolderSetCodecs.MobEffectRef.FieldOf("id").ForGetter((MobEffectInstance instance) => instance.Effect),
        Codecs.Int.OptionalFieldOf("duration", 0).ForGetter((MobEffectInstance instance) => instance.Duration),
        Codecs.Int.OptionalFieldOf("amplifier", 0).ForGetter((MobEffectInstance instance) => instance.Amplifier),
        Codecs.Bool.OptionalFieldOf("ambient", false).ForGetter((MobEffectInstance instance) => instance.IsAmbient),
        Codecs.Bool.OptionalFieldOf("show_particles", true).ForGetter((MobEffectInstance instance) => instance.IsVisible),
        (effect, duration, amplifier, ambient, visible)
            => new MobEffectInstance(effect, duration, amplifier, ambient, visible));

    //StreamCodec 网络编解码 效果 id 加时长与等级与标志位 对应原版 STREAM_CODEC
    public static readonly StreamCodec<RegistryFriendlyByteBuf, MobEffectInstance> StreamCodec =
        new MobEffectInstanceStreamCodec();

    public MobEffectInstance(Holder<NetCraft.Registry.MobEffect> effect, int duration, int amplifier, bool ambient, bool visible)
    {
        Effect = effect;
        Duration = duration;
        Amplifier = amplifier;
        IsAmbient = ambient;
        IsVisible = visible;
        //showIcon 对齐原版取 visible
        ShowIcon = visible;
    }

    //Effect 效果注册表引用 网络编解码按它取注册表 id
    public Holder<NetCraft.Registry.MobEffect> Effect { get; }

    //Duration 剩余刻数 -1 表示无限
    public int Duration { get; private set; }

    //Amplifier 效果等级 0 对应 I 级
    public int Amplifier { get; }

    //IsAmbient 是否环境效果 信标/潮涌核心施加的效果
    public bool IsAmbient { get; }

    //IsVisible 是否显示粒子
    public bool IsVisible { get; }

    //ShowIcon 是否显示图标
    public bool ShowIcon { get; }

    //Tick 每刻递减持续时间 -1 表示无限不减
    public void Tick()
    {
        if (Duration > 0) Duration--;
    }

    //Expired 持续时间归零 对应原版效果到期
    public bool Expired => Duration == 0;
}

//MobEffectInstanceStreamCodec 效果 id 加时长与等级与标志位 对应原版 STREAM_CODEC
internal sealed class MobEffectInstanceStreamCodec : StreamCodec<RegistryFriendlyByteBuf, MobEffectInstance>
{
    public MobEffectInstance Decode(RegistryFriendlyByteBuf buf)
    {
        var effectId = buf.ReadVarInt();
        var effect = BuiltInRegistries.MOB_EFFECT.Get(effectId)
            ?? throw new InvalidOperationException($"未知药水效果 id {effectId}");
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
        if (id < 0) throw new InvalidOperationException($"药水效果未注册: {value.Effect.Value}");
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
