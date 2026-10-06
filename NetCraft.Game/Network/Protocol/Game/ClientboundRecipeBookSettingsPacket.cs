namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundRecipeBookSettingsPacket recipe book settings packet, maps to vanilla ClientboundRecipeBookSettingsPacket
//Field: BookSettings(RecipeBookSettings)
public sealed record ClientboundRecipeBookSettingsPacket(object BookSettings) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundRecipeBookSettingsPacket> StreamCodec { get; } = new RecipeBookSettingsCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundRecipeBookSettings;

    public void Handle(ClientGamePacketListener handler) => handler.HandleRecipeBookSettings(this);

    private sealed class RecipeBookSettingsCodec : StreamCodec<FriendlyByteBuf, ClientboundRecipeBookSettingsPacket>
    {
        public ClientboundRecipeBookSettingsPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundRecipeBookSettingsPacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
