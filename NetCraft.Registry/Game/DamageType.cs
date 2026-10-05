using NetCraft.Codec;

namespace NetCraft.Registry;

//DamageType 伤害类型 对应原版 net.minecraft.world.damagesource.DamageType
//记录伤害的消息键 缩放规则 饥饿消耗 受伤表现与死亡消息类型
public sealed record DamageType(
    string MessageId,
    DamageScaling Scaling,
    float Exhaustion,
    DamageEffects Effects,
    DeathMessageType DeathMessageType)
{
    //Codec 持久化编解码 字段名 message_id scaling exhaustion effects death_message_type
    //effects 默认 HURT 死亡消息类型默认 DEFAULT 对应原版 DIRECT_CODEC
    public static readonly Codec<DamageType> Codec = RecordCodecBuilder.Of5(
        Codecs.String.FieldOf("message_id").ForGetter((DamageType type) => type.MessageId),
        DamageScalingCodecs.Codec.FieldOf("scaling").ForGetter((DamageType type) => type.Scaling),
        Codecs.Float.FieldOf("exhaustion").ForGetter((DamageType type) => type.Exhaustion),
        DamageEffectsCodecs.Codec.OptionalFieldOf("effects", DamageEffects.HURT)
            .ForGetter((DamageType type) => type.Effects),
        DeathMessageTypeCodecs.Codec.OptionalFieldOf("death_message_type", DeathMessageType.DEFAULT)
            .ForGetter((DamageType type) => type.DeathMessageType),
        (messageId, scaling, exhaustion, effects, deathMessageType) =>
            new DamageType(messageId, scaling, exhaustion, effects, deathMessageType));

    //只给缩放规则与消耗 对应原版三参构造
    public DamageType(string messageId, DamageScaling scaling, float exhaustion)
        : this(messageId, scaling, exhaustion, DamageEffects.HURT, DeathMessageType.DEFAULT) { }

    //再给受伤表现 对应原版四参构造
    public DamageType(string messageId, DamageScaling scaling, float exhaustion, DamageEffects effects)
        : this(messageId, scaling, exhaustion, effects, DeathMessageType.DEFAULT) { }

    //只给消耗与表现 缩放取 WHEN_CAUSED_BY_LIVING_NON_PLAYER 对应原版三参重载
    public DamageType(string messageId, float exhaustion, DamageEffects effects)
        : this(messageId, DamageScaling.WHEN_CAUSED_BY_LIVING_NON_PLAYER, exhaustion, effects) { }

    //只给消耗 对应原版两参构造
    public DamageType(string messageId, float exhaustion)
        : this(messageId, DamageScaling.WHEN_CAUSED_BY_LIVING_NON_PLAYER, exhaustion) { }
}
