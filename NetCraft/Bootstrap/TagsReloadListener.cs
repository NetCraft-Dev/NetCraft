using NetCraft.Logging;
using NetCraft.Registry;
using NetCraft.Resources;
using NetCraft.Tags;

namespace NetCraft.Bootstrap;

//TagsReloadListener 标签重载监听器包装 LoadBuiltinTags+BindAll
//每次 Reload 重新扫资源包 tag 文件并绑定到已冻结注册表
//必须在 BootstrapClass.BootStrap 之后调用因 BindAll 要求 Registry 已 Freeze
public sealed class TagsReloadListener : PreparableReloadListener
{
    private readonly TagManager _tagManager;
    private readonly RegistryAccess _registryAccess;

    public TagsReloadListener(TagManager tagManager, RegistryAccess registryAccess)
    {
        _tagManager = tagManager;
        _registryAccess = registryAccess;
    }

    //Reload 清空旧 loader 重扫 tag 文件并绑定到注册表
    //重载场景下 NamedHolderSet.Bind 会被覆盖原版行为一致
    public void Reload(ResourceManager rm, ReloadContext ctx)
    {
        //Log.Debug($"TagsReloadListener.Reload 入口 ctx={ctx.Name}");
        _tagManager.Reset();
        Bootstrap.LoadBuiltinTags(_tagManager, rm);
        _tagManager.BindAll(_registryAccess);
        //Log.Debug($"TagsReloadListener.Reload 出口");
    }
}
