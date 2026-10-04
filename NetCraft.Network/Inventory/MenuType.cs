using NetCraft.Registry;

namespace NetCraft.Network.Inventory;

//MenuType 菜单类型对应原版 net.minecraft.world.inventory.MenuType
//原版持有 MenuSupplier 委托和 FeatureFlagSet FeatureFlag 子系统未实现此处简化
//MENU 注册表用 object 弱类型避免跨层循环依赖 StreamCodec 手写通过 Lookup 查注册表 cast
//create 方法依赖 AbstractContainerMenu（Game 层）待容器子系统就绪后由 Game 层扩展
public sealed class MenuType
{
    //STREAM_CODEC 通过 MENU 注册表 id 编码 MenuType 对应原版 ByteBufCodecs.registry(Registries.MENU)
    //MENU 为 Registry<object> decode 时 cast object 为 MenuType encode 时 cast MenuType 为 object
    public static StreamCodec<RegistryFriendlyByteBuf, MenuType> StreamCodec { get; }
        = new MenuTypeRegistryCodec();

    //Create 构造 MenuType ResourceKey 对齐原版 ResourceKey.create
    public static ResourceKey<object> Create(string name)
        => ResourceKey<object>.Create(Registries.MENU, Identifier.WithDefaultNamespace(name));
}

//MenuTypeRegistryCodec 通过 MENU 注册表 id 编解码 MenuType
//对应原版 ByteBufCodecs.registry(Registries.MENU) 仅按 id 编码无 Direct 形式
//MENU 为 Registry<object> decode 时 cast object 为 MenuType encode 时 cast MenuType 为 object
internal sealed class MenuTypeRegistryCodec : StreamCodec<RegistryFriendlyByteBuf, MenuType>
{
    public MenuType Decode(RegistryFriendlyByteBuf buf)
    {
        int id = buf.ReadVarInt();
        var registry = buf.Lookup(Registries.MENU);
        var holder = registry.Get(id);
        if (holder is null)
            throw new InvalidOperationException($"未知 menu id {id} in MENU");
        return holder.Value as MenuType
            ?? throw new InvalidOperationException($"MENU 注册表值不是 MenuType: {holder.Value}");
    }

    public void Encode(RegistryFriendlyByteBuf buf, MenuType value)
    {
        var registry = buf.Lookup(Registries.MENU);
        int id = registry.GetId((object)value);
        if (id == IdMap<object>.Default)
            throw new InvalidOperationException($"menu 值未注册: {value}");
        buf.WriteVarInt(id);
    }
}
