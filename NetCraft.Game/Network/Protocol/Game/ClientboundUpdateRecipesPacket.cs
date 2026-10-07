namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundUpdateRecipesPacket recipes update packet, maps to vanilla ClientboundUpdateRecipesPacket
//Fields: ItemSets(Map<ResourceKey<RecipePropertySet>, RecipePropertySet>), StonecutterRecipes(SelectableRecipe.SingleInputSet<StonecutterRecipe>)
public sealed record ClientboundUpdateRecipesPacket(object ItemSets, object StonecutterRecipes) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundUpdateRecipesPacket> StreamCodec { get; } = new UpdateRecipesCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundUpdateRecipes;

    public void Handle(ClientGamePacketListener handler) => handler.HandleUpdateRecipes(this);

    private sealed class UpdateRecipesCodec : StreamCodec<FriendlyByteBuf, ClientboundUpdateRecipesPacket>
    {
        public ClientboundUpdateRecipesPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundUpdateRecipesPacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
