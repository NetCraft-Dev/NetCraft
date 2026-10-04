using NetCraft.Game.World.Items;
using NetCraft.Network;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetCursorItemPacket 光标物品包对应原版 ClientboundSetCursorItemPacket
//字段 Contents(ItemStack) 用 ItemStack.OptionalStreamCodec 编解码允许空栈
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
