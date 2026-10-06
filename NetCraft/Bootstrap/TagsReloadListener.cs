using NetCraft.Logging;
using NetCraft.Registry;
using NetCraft.Resources;
using NetCraft.Tags;

namespace NetCraft.Bootstrap;

//TagsReloadListener wraps LoadBuiltinTags+BindAll as a tag reload listener
//Each Reload rescans the resource pack tag files and binds them to the frozen registry
//Must be called after BootstrapClass.BootStrap because BindAll requires the Registry to be frozen
public sealed class TagsReloadListener : PreparableReloadListener
{
    private readonly TagManager _tagManager;
    private readonly RegistryAccess _registryAccess;

    public TagsReloadListener(TagManager tagManager, RegistryAccess registryAccess)
    {
        _tagManager = tagManager;
        _registryAccess = registryAccess;
    }

    //Reload clears the old loader, rescans the tag files and binds them to the registry
    //On reload, NamedHolderSet.Bind is overwritten, matching vanilla behavior
    public void Reload(ResourceManager rm, ReloadContext ctx)
    {
        //Log.Debug($"TagsReloadListener.Reload entry ctx={ctx.Name}");
        _tagManager.Reset();
        Bootstrap.LoadBuiltinTags(_tagManager, rm);
        _tagManager.BindAll(_registryAccess);
        //Log.Debug($"TagsReloadListener.Reload exit");
    }
}
