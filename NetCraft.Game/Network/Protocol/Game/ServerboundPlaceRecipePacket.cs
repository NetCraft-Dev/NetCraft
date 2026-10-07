namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundPlaceRecipePacket place recipe packet, maps to vanilla ServerboundPlaceRecipePacket
//Fields: ContainerId(int), Recipe(int recipe display ordinal), UseMaxItems(boolean)
public sealed record ServerboundPlaceRecipePacket(int ContainerId, int Recipe, bool UseMaxItems) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundPlaceRecipePacket> StreamCodec { get; } = new PlaceRecipeCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundPlaceRecipe;

    public void Handle(ServerGamePacketListener handler) => handler.HandlePlaceRecipe(this);

    private sealed class PlaceRecipeCodec : StreamCodec<FriendlyByteBuf, ServerboundPlaceRecipePacket>
    {
        //Sent when using one-click place from the recipe book; containerId and recipe are both VarInt
        public ServerboundPlaceRecipePacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt(), buf.ReadVarInt(), buf.ReadBoolean());

        public void Encode(FriendlyByteBuf buf, ServerboundPlaceRecipePacket value)
        {
            buf.WriteVarInt(value.ContainerId);
            buf.WriteVarInt(value.Recipe);
            buf.WriteBoolean(value.UseMaxItems);
        }
    }
}
