using NetCraft.Codec;

namespace NetCraft.Registry;

//DeathMessageType death message type, maps to vanilla net.minecraft.world.damagesource.DeathMessageType
public enum DeathMessageType
{
    DEFAULT,
    FALL_VARIANTS,
    INTENTIONAL_GAME_DESIGN
}

//DeathMessageTypeCodecs serialized names and codecs for the death message type
public static class DeathMessageTypeCodecs
{
    //GetName serialized name, maps to vanilla getSerializedName
    public static string GetName(this DeathMessageType type) => type switch
    {
        DeathMessageType.DEFAULT => "default",
        DeathMessageType.FALL_VARIANTS => "fall_variants",
        _ => "intentional_game_design"
    };

    //Codec persistence codec by serialized name, maps to vanilla CODEC
    public static readonly Codec<DeathMessageType> Codec = Codecs.String.ComapFlatMap(
        name => name switch
        {
            "default" => DataResult<DeathMessageType>.Success(DeathMessageType.DEFAULT),
            "fall_variants" => DataResult<DeathMessageType>.Success(DeathMessageType.FALL_VARIANTS),
            "intentional_game_design" => DataResult<DeathMessageType>.Success(DeathMessageType.INTENTIONAL_GAME_DESIGN),
            _ => DataResult<DeathMessageType>.Error(() => $"Unknown death message type {name}")
        },
        type => type.GetName());
}
