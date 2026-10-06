using NetCraft.Game.World.Items;
using NetCraft.Network;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundContainerSetContentPacket container content set packet, maps to vanilla ClientboundContainerSetContentPacket
//Fields: ContainerId(VarInt), StateId(VarInt), Items(List<ItemStack>), CarriedItem(ItemStack)
//Items is encoded/decoded with ByteBufCodecs.Collection(ItemStack.OptionalStreamCodec)
//CarriedItem uses OptionalStreamCodec directly; an empty stack writes count=0 without a boolean prefix
public sealed record ClientboundContainerSetContentPacket(int ContainerId, int StateId, List<ItemStack> Items, ItemStack CarriedItem) : Packet<ClientGamePacketListener>
{
    private static readonly StreamCodec<RegistryFriendlyByteBuf, List<ItemStack>> _itemsCodec
        = ByteBufCodecs.Collection(ItemStack.OptionalStreamCodec);

    public static StreamCodec<RegistryFriendlyByteBuf, ClientboundContainerSetContentPacket> StreamCodec { get; } = new ContainerSetContentCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundContainerSetContent;

    public void Handle(ClientGamePacketListener handler) => handler.HandleContainerContent(this);

    private sealed class ContainerSetContentCodec : StreamCodec<RegistryFriendlyByteBuf, ClientboundContainerSetContentPacket>
    {
        public ClientboundContainerSetContentPacket Decode(RegistryFriendlyByteBuf buf)
        {
            int containerId = buf.ReadVarInt();
            int stateId = buf.ReadVarInt();
            var items = _itemsCodec.Decode(buf);
            var carried = ItemStack.OptionalStreamCodec.Decode(buf);
            return new(containerId, stateId, items, carried);
        }

        public void Encode(RegistryFriendlyByteBuf buf, ClientboundContainerSetContentPacket value)
        {
            buf.WriteVarInt(value.ContainerId);
            buf.WriteVarInt(value.StateId);
            _itemsCodec.Encode(buf, value.Items);
            ItemStack.OptionalStreamCodec.Encode(buf, value.CarriedItem);
        }
    }
}
