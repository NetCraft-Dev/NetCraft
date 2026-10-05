using NetCraft.Codec;

namespace NetCraft.Registry;

//DeathMessageType 死亡消息类型 对应原版 net.minecraft.world.damagesource.DeathMessageType
public enum DeathMessageType
{
    DEFAULT,
    FALL_VARIANTS,
    INTENTIONAL_GAME_DESIGN
}

//DeathMessageTypeCodecs 死亡消息类型的序列化名与编解码
public static class DeathMessageTypeCodecs
{
    //GetName 序列化名 对应原版 getSerializedName
    public static string GetName(this DeathMessageType type) => type switch
    {
        DeathMessageType.DEFAULT => "default",
        DeathMessageType.FALL_VARIANTS => "fall_variants",
        _ => "intentional_game_design"
    };

    //Codec 持久化编解码 按序列化名 对应原版 CODEC
    public static readonly Codec<DeathMessageType> Codec = Codecs.String.ComapFlatMap(
        name => name switch
        {
            "default" => DataResult<DeathMessageType>.Success(DeathMessageType.DEFAULT),
            "fall_variants" => DataResult<DeathMessageType>.Success(DeathMessageType.FALL_VARIANTS),
            "intentional_game_design" => DataResult<DeathMessageType>.Success(DeathMessageType.INTENTIONAL_GAME_DESIGN),
            _ => DataResult<DeathMessageType>.Error(() => $"未知死亡消息类型 {name}")
        },
        type => type.GetName());
}
