using NetCraft.Game.World.Items;
using NetCraft.Network;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundSetPlayerInventoryPacket 玩家背包设置包对应原版 ClientboundSetPlayerInventoryPacket
//字段 Slot(int) Contents(ItemStack) Slot VarInt Contents ItemStack.OptionalStreamCodec
public sealed record ClientboundSetPlayerInventoryPacket(int Slot, ItemStack Contents) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<RegistryFriendlyByteBuf, ClientboundSetPlayerInventoryPacket> StreamCodec { get; } = new SetPlayerInventoryCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundSetPlayerInventory;

    public void Handle(ClientGamePacketListener handler) => handler.HandleSetPlayerInventory(this);

    private sealed class SetPlayerInventoryCodec : StreamCodec<RegistryFriendlyByteBuf, ClientboundSetPlayerInventoryPacket>
    {
        public ClientboundSetPlayerInventoryPacket Decode(RegistryFriendlyByteBuf buf)
            => new(buf.ReadVarInt(), ItemStack.OptionalStreamCodec.Decode(buf));

        public void Encode(RegistryFriendlyByteBuf buf, ClientboundSetPlayerInventoryPacket value)
        {
            buf.WriteVarInt(value.Slot);
            ItemStack.OptionalStreamCodec.Encode(buf, value.Contents);
        }
    }
}
