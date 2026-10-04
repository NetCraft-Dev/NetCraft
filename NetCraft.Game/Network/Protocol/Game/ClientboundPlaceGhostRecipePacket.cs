namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundPlaceGhostRecipePacket 占位配方包对应原版 ClientboundPlaceGhostRecipePacket
//字段 ContainerId(int) RecipeDisplay(RecipeDisplay)
public sealed record ClientboundPlaceGhostRecipePacket(int ContainerId, object RecipeDisplay) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundPlaceGhostRecipePacket> StreamCodec { get; } = new PlaceGhostRecipeCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundPlaceGhostRecipe;

    public void Handle(ClientGamePacketListener handler) => handler.HandlePlaceRecipe(this);

    private sealed class PlaceGhostRecipeCodec : StreamCodec<FriendlyByteBuf, ClientboundPlaceGhostRecipePacket>
    {
        public ClientboundPlaceGhostRecipePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundPlaceGhostRecipePacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
