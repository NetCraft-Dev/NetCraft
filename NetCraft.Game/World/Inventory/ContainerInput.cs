using NetCraft.Network;

namespace NetCraft.Game.World.Inventory;

//ContainerInput container click type, maps to vanilla net.minecraft.world.inventory.ContainerInput
//The click intent reported by the client, the server takes a different move branch per type
public enum ContainerInput
{
    //Pickup left click pick up/place, right click place half
    Pickup = 0,
    //QuickMove shift-click, moves between the two slot ranges
    QuickMove = 1,
    //Swap number keys or F swap with the hotbar/offhand
    Swap = 2,
    //Clone creative middle-click clone
    Clone = 3,
    //Throw drop (Q drops one, ctrl+Q drops the stack)
    Throw = 4,
    //QuickCraft drag-distribute while holding left/right click
    QuickCraft = 5,
    //PickupAll double-click collect same items
    PickupAll = 6,
}

//ContainerInputCodec encodes/decodes VarInt by enum id, matches vanilla ByteBufCodecs.idMapper
public sealed class ContainerInputCodec : StreamCodec<FriendlyByteBuf, ContainerInput>
{
    //StreamCodec static instance, the generic parameter B is contravariant so a parent-type buf codec works directly with RegistryFriendlyByteBuf
    public static readonly StreamCodec<RegistryFriendlyByteBuf, ContainerInput> StreamCodec = new ContainerInputCodec();

    public ContainerInput Decode(FriendlyByteBuf buf)
    {
        var id = buf.ReadVarInt();
        return Enum.IsDefined(typeof(ContainerInput), id) ? (ContainerInput)id : ContainerInput.Pickup;
    }

    public void Encode(FriendlyByteBuf buf, ContainerInput value) => buf.WriteVarInt((int)value);
}
