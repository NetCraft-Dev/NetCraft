namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundRecipeBookSeenRecipePacket recipe book seen recipe packet, maps to vanilla ServerboundRecipeBookSeenRecipePacket
//Field: Recipe(int recipe display ordinal)
public sealed record ServerboundRecipeBookSeenRecipePacket(int Recipe) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundRecipeBookSeenRecipePacket> StreamCodec { get; } = new RecipeBookSeenRecipeCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundRecipeBookSeenRecipe;

    public void Handle(ServerGamePacketListener handler) => handler.HandleRecipeBookSeenRecipePacket(this);

    private sealed class RecipeBookSeenRecipeCodec : StreamCodec<FriendlyByteBuf, ServerboundRecipeBookSeenRecipePacket>
    {
        //Reported as seen after the client opens a recipe; the vanilla RecipeDisplayId is just a single varint ordinal
        public ServerboundRecipeBookSeenRecipePacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt());

        public void Encode(FriendlyByteBuf buf, ServerboundRecipeBookSeenRecipePacket value)
            => buf.WriteVarInt(value.Recipe);
    }
}
