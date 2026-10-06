namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundPlaceGhostRecipePacket place ghost recipe packet, maps to vanilla ClientboundPlaceGhostRecipePacket
//Fields: ContainerId(int), RecipeDisplay(RecipeDisplay)
public sealed record ClientboundPlaceGhostRecipePacket(int ContainerId, object RecipeDisplay) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundPlaceGhostRecipePacket> StreamCodec { get; } = new PlaceGhostRecipeCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundPlaceGhostRecipe;

    public void Handle(ClientGamePacketListener handler) => handler.HandlePlaceRecipe(this);

    private sealed class PlaceGhostRecipeCodec : StreamCodec<FriendlyByteBuf, ClientboundPlaceGhostRecipePacket>
    {
        public ClientboundPlaceGhostRecipePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("Business type not yet implemented");

        public void Encode(FriendlyByteBuf buf, ClientboundPlaceGhostRecipePacket value)
            => throw new NotImplementedException("Business type not yet implemented");
    }
}
