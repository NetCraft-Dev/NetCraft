namespace NetCraft.Game.Network.Protocol.Game;

//ServerboundRecipeBookChangeSettingsPacket recipe book change settings packet, maps to vanilla ServerboundRecipeBookChangeSettingsPacket
//Fields: BookType (vanilla RecipeBookType enum ordinal), IsOpen(boolean), IsFiltering(boolean)
//nc has no recipe book system yet; BookType stores the ordinal and the listener side is a no-op
public sealed record ServerboundRecipeBookChangeSettingsPacket(int BookType, bool IsOpen, bool IsFiltering) : Packet<ServerGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ServerboundRecipeBookChangeSettingsPacket> StreamCodec { get; } = new RecipeBookChangeSettingsCodec();

    public PacketType<ServerGamePacketListener> Type => GamePacketTypes.ServerboundRecipeBookChangeSettings;

    public void Handle(ServerGamePacketListener handler) => handler.HandleRecipeBookChangeSettingsPacket(this);

    private sealed class RecipeBookChangeSettingsCodec : StreamCodec<FriendlyByteBuf, ServerboundRecipeBookChangeSettingsPacket>
    {
        //Vanilla is composite(RecipeBookType varint ordinal, bool, bool)
        //The client sends this both when opening and closing the recipe book screen; if not registered it spams unknown-packet warnings in the server log
        public ServerboundRecipeBookChangeSettingsPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadVarInt(), buf.ReadBoolean(), buf.ReadBoolean());

        public void Encode(FriendlyByteBuf buf, ServerboundRecipeBookChangeSettingsPacket value)
        {
            buf.WriteVarInt(value.BookType);
            buf.WriteBoolean(value.IsOpen);
            buf.WriteBoolean(value.IsFiltering);
        }
    }
}
