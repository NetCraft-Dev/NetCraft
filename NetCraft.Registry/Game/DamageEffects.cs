using NetCraft.Codec;

namespace NetCraft.Registry;

//DamageEffects hurt effect, maps to vanilla net.minecraft.world.damagesource.DamageEffects
//Each vanilla entry also carries a sound; the sound system is not wired up here yet, so only the serialized names are kept
public enum DamageEffects
{
    HURT,
    THORNS,
    DROWNING,
    BURNING,
    POKING,
    FREEZING
}

//DamageEffectsCodecs serialized names and codecs for hurt effects
public static class DamageEffectsCodecs
{
    //GetName serialized name, maps to vanilla getSerializedName
    public static string GetName(this DamageEffects effects) => effects switch
    {
        DamageEffects.HURT => "hurt",
        DamageEffects.THORNS => "thorns",
        DamageEffects.DROWNING => "drowning",
        DamageEffects.BURNING => "burning",
        DamageEffects.POKING => "poking",
        _ => "freezing"
    };

    //Codec persistence codec by serialized name, maps to vanilla CODEC
    public static readonly Codec<DamageEffects> Codec = Codecs.String.ComapFlatMap(
        name => name switch
        {
            "hurt" => DataResult<DamageEffects>.Success(DamageEffects.HURT),
            "thorns" => DataResult<DamageEffects>.Success(DamageEffects.THORNS),
            "drowning" => DataResult<DamageEffects>.Success(DamageEffects.DROWNING),
            "burning" => DataResult<DamageEffects>.Success(DamageEffects.BURNING),
            "poking" => DataResult<DamageEffects>.Success(DamageEffects.POKING),
            "freezing" => DataResult<DamageEffects>.Success(DamageEffects.FREEZING),
            _ => DataResult<DamageEffects>.Error(() => $"Unknown hurt effect {name}")
        },
        effects => effects.GetName());
}
