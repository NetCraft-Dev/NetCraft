using NetCraft.Codec;

namespace NetCraft.Registry;

//DamageScaling damage scaling rule, maps to vanilla net.minecraft.world.damagesource.DamageScaling
//Determines whether the damage value scales with difficulty
public enum DamageScaling
{
    WHEN_CAUSED_BY_LIVING_NON_PLAYER,
    ALWAYS,
    NEVER
}

//DamageScalingCodecs serialized names and codecs for the damage scaling rule
public static class DamageScalingCodecs
{
    //GetName serialized name, maps to vanilla getSerializedName
    public static string GetName(this DamageScaling scaling) => scaling switch
    {
        DamageScaling.WHEN_CAUSED_BY_LIVING_NON_PLAYER => "when_caused_by_living_non_player",
        DamageScaling.ALWAYS => "always",
        _ => "never"
    };

    //Codec persistence codec by serialized name, maps to vanilla CODEC
    public static readonly Codec<DamageScaling> Codec = Codecs.String.ComapFlatMap(
        name => name switch
        {
            "when_caused_by_living_non_player" =>
                DataResult<DamageScaling>.Success(DamageScaling.WHEN_CAUSED_BY_LIVING_NON_PLAYER),
            "always" => DataResult<DamageScaling>.Success(DamageScaling.ALWAYS),
            "never" => DataResult<DamageScaling>.Success(DamageScaling.NEVER),
            _ => DataResult<DamageScaling>.Error(() => $"Unknown damage scaling {name}")
        },
        scaling => scaling.GetName());
}
