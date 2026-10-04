using NetCraft.Network;

namespace NetCraft.Game.World.Inventory;

//ContainerInput 容器点击类型对应原版 net.minecraft.world.inventory.ContainerInput
//客户端上报的点击意图 服务端按类型走不同的搬运分支
public enum ContainerInput
{
    //Pickup 左键拾取/放下 右键分半放下
    Pickup = 0,
    //QuickMove 按住 shift 点击 在两段槽位区间之间搬运
    QuickMove = 1,
    //Swap 数字键或 F 与快捷栏/副手交换
    Swap = 2,
    //Clone 创造模式中键复制
    Clone = 3,
    //Throw 丢弃(Q 丢一个 ctrl+Q 丢一组)
    Throw = 4,
    //QuickCraft 按住左/右键拖动分配
    QuickCraft = 5,
    //PickupAll 双击收集同种物品
    PickupAll = 6,
}

//ContainerInputCodec 按枚举 id 编解码 VarInt 对齐原版 ByteBufCodecs.idMapper
public sealed class ContainerInputCodec : StreamCodec<FriendlyByteBuf, ContainerInput>
{
    //StreamCodec 静态实例 泛型参数 B 逆变 父类型 buf 的 codec 可直接用于 RegistryFriendlyByteBuf
    public static readonly StreamCodec<RegistryFriendlyByteBuf, ContainerInput> StreamCodec = new ContainerInputCodec();

    public ContainerInput Decode(FriendlyByteBuf buf)
    {
        var id = buf.ReadVarInt();
        return Enum.IsDefined(typeof(ContainerInput), id) ? (ContainerInput)id : ContainerInput.Pickup;
    }

    public void Encode(FriendlyByteBuf buf, ContainerInput value) => buf.WriteVarInt((int)value);
}
