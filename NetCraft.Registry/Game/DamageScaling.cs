using NetCraft.Codec;

namespace NetCraft.Registry;

//DamageScaling 伤害缩放规则 对应原版 net.minecraft.world.damagesource.DamageScaling
//决定伤害值是否按难度缩放
public enum DamageScaling
{
    WHEN_CAUSED_BY_LIVING_NON_PLAYER,
    ALWAYS,
    NEVER
}

//DamageScalingCodecs 伤害缩放规则的序列化名与编解码
public static class DamageScalingCodecs
{
    //GetName 序列化名 对应原版 getSerializedName
    public static string GetName(this DamageScaling scaling) => scaling switch
    {
        DamageScaling.WHEN_CAUSED_BY_LIVING_NON_PLAYER => "when_caused_by_living_non_player",
        DamageScaling.ALWAYS => "always",
        _ => "never"
    };

    //Codec 持久化编解码 按序列化名 对应原版 CODEC
    public static readonly Codec<DamageScaling> Codec = Codecs.String.ComapFlatMap(
        name => name switch
        {
            "when_caused_by_living_non_player" =>
                DataResult<DamageScaling>.Success(DamageScaling.WHEN_CAUSED_BY_LIVING_NON_PLAYER),
            "always" => DataResult<DamageScaling>.Success(DamageScaling.ALWAYS),
            "never" => DataResult<DamageScaling>.Success(DamageScaling.NEVER),
            _ => DataResult<DamageScaling>.Error(() => $"未知伤害缩放 {name}")
        },
        scaling => scaling.GetName());
}
