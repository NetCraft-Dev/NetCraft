using NetCraft.Logging;
using NetCraft.Resources;

namespace NetCraft.Game.World.Crafting;

//RecipeReloadListener recipe reload listener, maps to the hook attaching vanilla RecipeManager into ReloadableServerResources
//Every reload re-reads the datapack recipe directory into RecipeManager
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
