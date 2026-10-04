namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundPlaceRecipePacket 数据包对应原版 ServerboundPlaceRecipePacket
//字段 ContainerId(int) Recipe(int 配方展示序号) UseMaxItems(boolean)
public sealed record ServerboundPlaceRecipePacket(int ContainerId, int Recipe, bool UseMaxItems) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundPlaceRecipePacket> StreamCodec { get; } = new PlaceRecipeCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundPlaceRecipe;

    public void Handle(ServerGamePacketListener handler) => handler.HandlePlaceRecipe(this);

    private sealed class PlaceRecipeCodec : StreamCodec<FriendlyByteBuf, ServerboundPlaceRecipePacket>
    {
        //配方书一键摆放时发送 containerId 与 recipe 都是 VarInt
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
