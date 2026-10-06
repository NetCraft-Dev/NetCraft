using NetCraft.Codec;

namespace NetCraft.Registry;

//DamageType damage type, maps to vanilla net.minecraft.world.damagesource.DamageType
//Records the damage message key, scaling rule, exhaustion, hurt effect and death message type
public sealed record DamageType(
    string MessageId,
    DamageScaling Scaling,
    float Exhaustion,
    DamageEffects Effects,
    DeathMessageType DeathMessageType)
{
    //Codec persistence codec with fields message_id scaling exhaustion effects death_message_type
    //effects defaults to HURT and the death message type to DEFAULT, maps to vanilla DIRECT_CODEC
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

    //Only scaling rule and exhaustion, maps to the vanilla three-argument constructor
    public DamageType(string messageId, DamageScaling scaling, float exhaustion)
        : this(messageId, scaling, exhaustion, DamageEffects.HURT, DeathMessageType.DEFAULT) { }

    //Also provides the hurt effect, maps to the vanilla four-argument constructor
    public DamageType(string messageId, DamageScaling scaling, float exhaustion, DamageEffects effects)
        : this(messageId, scaling, exhaustion, effects, DeathMessageType.DEFAULT) { }

    //Only exhaustion and effect, with scaling set to WHEN_CAUSED_BY_LIVING_NON_PLAYER, maps to the vanilla three-argument overload
    public DamageType(string messageId, float exhaustion, DamageEffects effects)
        : this(messageId, DamageScaling.WHEN_CAUSED_BY_LIVING_NON_PLAYER, exhaustion, effects) { }

    //Only exhaustion, maps to the vanilla two-argument constructor
    public DamageType(string messageId, float exhaustion)
        : this(messageId, DamageScaling.WHEN_CAUSED_BY_LIVING_NON_PLAYER, exhaustion) { }
}
