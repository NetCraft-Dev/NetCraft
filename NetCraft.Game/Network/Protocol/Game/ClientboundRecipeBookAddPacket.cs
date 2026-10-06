namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundRecipeBookAddPacket recipe book add packet, maps to vanilla ClientboundRecipeBookAddPacket
//Fields: Entries(List<Entry>), Replace(boolean)
public sealed record ClientboundRecipeBookAddPacket(object Entries, bool Replace) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundRecipeBookAddPacket> StreamCodec { get; } = new RecipeBookAddCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundRecipeBookAdd;

    public void Handle(ClientGamePacketListener handler) => handler.HandleRecipeBookAdd(this);

    private sealed class RecipeBookAddCodec : StreamCodec<FriendlyByteBuf, ClientboundRecipeBookAddPacket>
    {
        public ClientboundRecipeBookAddPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundRecipeBookAddPacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
