namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundRecipeBookSettingsPacket 配方书设置包对应原版 ClientboundRecipeBookSettingsPacket
//字段 BookSettings(RecipeBookSettings)
public sealed record ClientboundRecipeBookSettingsPacket(object BookSettings) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundRecipeBookSettingsPacket> StreamCodec { get; } = new RecipeBookSettingsCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundRecipeBookSettings;

    public void Handle(ClientGamePacketListener handler) => handler.HandleRecipeBookSettings(this);

    private sealed class RecipeBookSettingsCodec : StreamCodec<FriendlyByteBuf, ClientboundRecipeBookSettingsPacket>
    {
        public ClientboundRecipeBookSettingsPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundRecipeBookSettingsPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
