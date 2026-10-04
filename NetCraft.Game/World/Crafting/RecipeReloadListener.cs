using NetCraft.Logging;
using NetCraft.Resources;

namespace NetCraft.Game.World.Crafting;

//RecipeReloadListener 配方重载监听 对应原版 RecipeManager 挂进 ReloadableServerResources 的那一环
//每次重载把数据包 recipe 目录重新读进 RecipeManager
public sealed class RecipeReloadListener : PreparableReloadListener
{
    private readonly RecipeManager _recipes;

    public RecipeReloadListener(RecipeManager recipes) => _recipes = recipes;

    public void Reload(ResourceManager resourceManager, ReloadContext context)
    {
        var result = _recipes.Load(resourceManager);
        Log.Info($"Recipe reload finished: {result.LoadedCount} recipes, {result.Errors.Count} errors");
        foreach (var error in result.Errors) Log.Warning($"Recipe failed to load {error}");
    }
}
