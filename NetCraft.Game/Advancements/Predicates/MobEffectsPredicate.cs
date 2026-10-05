using NetCraft.Codec;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.World.Effect;
using NetCraft.Registry;

namespace NetCraft.Game.Advancements.Predicates;

//MobEffectsPredicate 效果谓词 逐条判定实体身上的药水效果
//对应原版 net.minecraft.advancements.predicates.MobEffectsPredicate
public sealed record MobEffectsPredicate(
    Dictionary<Holder<NetCraft.Registry.MobEffect>, MobEffectsPredicate.MobEffectInstancePredicate> EffectMap)
{
    //Codec 效果引用到实例谓词的映射 对应原版 CODEC
    public static readonly Codec<MobEffectsPredicate> Codec = Codecs.UnboundedMap(
        HolderSetCodecs.MobEffectRef, MobEffectInstancePredicate.Codec).ComapFlatMap(
        map => DataResult<MobEffectsPredicate>.Success(new MobEffectsPredicate(map)),
        predicate => predicate.EffectMap);

    //Matches 期望的每条效果都必须存在且实例满足谓词 对应原版 matches
    public bool Matches(IReadOnlyDictionary<Holder<NetCraft.Registry.MobEffect>, MobEffectInstance> effects)
    {
        foreach (var entry in EffectMap)
        {
            effects.TryGetValue(entry.Key, out var instance);
            if (!entry.Value.Matches(instance)) return false;
        }
        return true;
    }

    //MobEffectInstancePredicate 单条效果实例谓词 等级与时长区间加环境与可见标志
    public sealed record MobEffectInstancePredicate(
        MinMaxBounds.Ints Amplifier,
        MinMaxBounds.Ints Duration,
        Optional<bool> Ambient,
        Optional<bool> Visible)
    {
        //Codec 持久化编解码 字段名 amplifier duration ambient visible 对应原版 CODEC
        public static readonly Codec<MobEffectInstancePredicate> Codec = RecordCodecBuilder.Of4(
            MinMaxBounds.Ints.CODEC.OptionalFieldOf("amplifier", MinMaxBounds.Ints.Any)
                .ForGetter((MobEffectInstancePredicate predicate) => predicate.Amplifier),
            MinMaxBounds.Ints.CODEC.OptionalFieldOf("duration", MinMaxBounds.Ints.Any)
                .ForGetter((MobEffectInstancePredicate predicate) => predicate.Duration),
            Codecs.Bool.OptionalFieldOf("ambient")
                .ForGetter((MobEffectInstancePredicate predicate) => predicate.Ambient),
            Codecs.Bool.OptionalFieldOf("visible")
                .ForGetter((MobEffectInstancePredicate predicate) => predicate.Visible),
            (amplifier, duration, ambient, visible)
                => new MobEffectInstancePredicate(amplifier, duration, ambient, visible));

        //Matches 实例缺失即不匹配 给出期望的项才约束 对应原版 matches
        public bool Matches(MobEffectInstance? instance)
            => instance is not null
                && Amplifier.Matches(instance.Amplifier)
                && Duration.Matches(instance.Duration)
                && (!Ambient.IsPresent || Ambient.Get() == instance.IsAmbient)
                && (!Visible.IsPresent || Visible.Get() == instance.IsVisible);
    }
}
