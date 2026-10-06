namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundRecipeBookRemovePacket recipe book remove packet, maps to vanilla ClientboundRecipeBookRemovePacket
//Field: Recipes(List<RecipeDisplayId>)
public sealed record ClientboundRecipeBookRemovePacket(object Recipes) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundRecipeBookRemovePacket> StreamCodec { get; } = new RecipeBookRemoveCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundRecipeBookRemove;

    public void Handle(ClientGamePacketListener handler) => handler.HandleRecipeBookRemove(this);

    private sealed class RecipeBookRemoveCodec : StreamCodec<FriendlyByteBuf, ClientboundRecipeBookRemovePacket>
    {
        public ClientboundRecipeBookRemovePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundRecipeBookRemovePacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
