namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundRecipeBookAddPacket 配方书添加包对应原版 ClientboundRecipeBookAddPacket
//字段 Entries(List<Entry>) Replace(boolean)
public sealed record ClientboundRecipeBookAddPacket(object Entries, bool Replace) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundRecipeBookAddPacket> StreamCodec { get; } = new RecipeBookAddCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundRecipeBookAdd;

    public void Handle(ClientGamePacketListener handler) => handler.HandleRecipeBookAdd(this);

    private sealed class RecipeBookAddCodec : StreamCodec<FriendlyByteBuf, ClientboundRecipeBookAddPacket>
    {
        public ClientboundRecipeBookAddPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundRecipeBookAddPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
