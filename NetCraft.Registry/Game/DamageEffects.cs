using NetCraft.Codec;

namespace NetCraft.Registry;

//DamageEffects 受伤表现 对应原版 net.minecraft.world.damagesource.DamageEffects
//原版每项还带一个音效 本作音效体系未接通 只保留序列化名
public enum DamageEffects
{
    HURT,
    THORNS,
    DROWNING,
    BURNING,
    POKING,
    FREEZING
}

//DamageEffectsCodecs 受伤表现的序列化名与编解码
public static class DamageEffectsCodecs
{
    //GetName 序列化名 对应原版 getSerializedName
    public static string GetName(this DamageEffects effects) => effects switch
    {
        DamageEffects.HURT => "hurt",
        DamageEffects.THORNS => "thorns",
        DamageEffects.DROWNING => "drowning",
        DamageEffects.BURNING => "burning",
        DamageEffects.POKING => "poking",
        _ => "freezing"
    };

    //Codec 持久化编解码 按序列化名 对应原版 CODEC
    public static readonly Codec<DamageEffects> Codec = Codecs.String.ComapFlatMap(
        name => name switch
        {
            "hurt" => DataResult<DamageEffects>.Success(DamageEffects.HURT),
            "thorns" => DataResult<DamageEffects>.Success(DamageEffects.THORNS),
            "drowning" => DataResult<DamageEffects>.Success(DamageEffects.DROWNING),
            "burning" => DataResult<DamageEffects>.Success(DamageEffects.BURNING),
            "poking" => DataResult<DamageEffects>.Success(DamageEffects.POKING),
            "freezing" => DataResult<DamageEffects>.Success(DamageEffects.FREEZING),
            _ => DataResult<DamageEffects>.Error(() => $"未知受伤表现 {name}")
        },
        effects => effects.GetName());
}
