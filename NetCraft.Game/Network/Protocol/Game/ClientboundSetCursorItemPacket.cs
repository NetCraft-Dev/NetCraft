using NetCraft.Game.World.Items;
using NetCraft.Network;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetCursorItemPacket cursor item packet, maps to vanilla ClientboundSetCursorItemPacket
//Field: Contents(ItemStack), encoded with ItemStack.OptionalStreamCodec to allow an empty stack
public sealed record ClientboundSetCursorItemPacket(ItemStack Contents) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<RegistryFriendlyByteBuf, ClientboundSetCursorItemPacket> StreamCodec { get; } = new SetCursorItemCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetCursorItem;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSetCursorItem(this);

    private sealed class SetCursorItemCodec : StreamCodec<RegistryFriendlyByteBuf, ClientboundSetCursorItemPacket>
    {
        public ClientboundSetCursorItemPacket Decode(RegistryFriendlyByteBuf buf)
            => new(ItemStack.OptionalStreamCodec.Decode(buf));

        public void Encode(RegistryFriendlyByteBuf buf, ClientboundSetCursorItemPacket value)
            => ItemStack.OptionalStreamCodec.Encode(buf, value.Contents);
    }
}
