namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundRecipeBookRemovePacket 配方书移除包对应原版 ClientboundRecipeBookRemovePacket
//字段 Recipes(List<RecipeDisplayId>)
public sealed record ClientboundRecipeBookRemovePacket(object Recipes) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundRecipeBookRemovePacket> StreamCodec { get; } = new RecipeBookRemoveCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundRecipeBookRemove;

    public void Handle(ClientGamePacketListener handler) => handler.HandleRecipeBookRemove(this);

    private sealed class RecipeBookRemoveCodec : StreamCodec<FriendlyByteBuf, ClientboundRecipeBookRemovePacket>
    {
        public ClientboundRecipeBookRemovePacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundRecipeBookRemovePacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
