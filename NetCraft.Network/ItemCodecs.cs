using NetCraft.Registry;

namespace NetCraft.Network;

//ItemCodecs Item 相关网络编解码对应原版 Item.STREAM_CODEC 静态字段
//原版 Item.STREAM_CODEC 在 Item 类内 NetCraft 因 Registry 不依赖 Network 放 Network 子库
//HolderCodec 从 RegistryFriendlyByteBuf 读 id 转 Holder<Item>
public static class ItemCodecs
{
    //StreamCodec Holder<Item> 编解码用 ByteBufCodecs.Holder 从 ITEM 注册表查
    public static readonly StreamCodec<RegistryFriendlyByteBuf, Holder<Item>> StreamCodec
        = ByteBufCodecs.Holder(Registries.ITEM);
}
