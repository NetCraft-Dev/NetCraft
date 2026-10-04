namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundUpdateRecipesPacket 配方更新包对应原版 ClientboundUpdateRecipesPacket
//字段 ItemSets(Map<ResourceKey<RecipePropertySet>, RecipePropertySet>) StonecutterRecipes(SelectableRecipe.SingleInputSet<StonecutterRecipe>)
public sealed record ClientboundUpdateRecipesPacket(object ItemSets, object StonecutterRecipes) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundUpdateRecipesPacket> StreamCodec { get; } = new UpdateRecipesCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundUpdateRecipes;

    public void Handle(ClientGamePacketListener handler) => handler.HandleUpdateRecipes(this);

    private sealed class UpdateRecipesCodec : StreamCodec<FriendlyByteBuf, ClientboundUpdateRecipesPacket>
    {
        public ClientboundUpdateRecipesPacket Decode(FriendlyByteBuf buf)
            => throw new NotImplementedException("业务类型待实现");

        public void Encode(FriendlyByteBuf buf, ClientboundUpdateRecipesPacket value)
            => throw new NotImplementedException("业务类型待实现");
    }
}
