namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundRecipeBookSeenRecipePacket 数据包对应原版 ServerboundRecipeBookSeenRecipePacket
//字段 Recipe(int 配方展示序号)
public sealed record ServerboundRecipeBookSeenRecipePacket(int Recipe) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundRecipeBookSeenRecipePacket> StreamCodec { get; } = new RecipeBookSeenRecipeCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundRecipeBookSeenRecipe;

    public void Handle(ServerGamePacketListener handler) => handler.HandleRecipeBookSeenRecipePacket(this);

    private sealed class RecipeBookSeenRecipeCodec : StreamCodec<FriendlyByteBuf, ServerboundRecipeBookSeenRecipePacket>
    {
        //客户端点开一条配方后上报已查看 原版 RecipeDisplayId 就是单个 varint 序号
        public ServerboundRecipeBookSeenRecipePacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ServerboundRecipeBookSeenRecipePacket value)
            => buf.WriteVarInt(value.Recipe);
    }
}
