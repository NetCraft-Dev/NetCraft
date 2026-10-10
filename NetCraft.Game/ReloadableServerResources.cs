using NetCraft.Bootstrap;
using NetCraft.Game.Server;
using NetCraft.Game.World.Crafting;
using NetCraft.Game.World.Level.Block;
using NetCraft.Logging;
using NetCraft.Registry;
using NetCraft.Resources;
using NetCraft.Registry;

namespace NetCraft.Game;

//ReloadableServerResources server-side reloadable resource set, a trimmed version of the vanilla same-named class
//Holds the ResourceManager and the registered listener list; LoadResources builds everything at once and triggers the first reload
//Omits business listeners such as Advancements/Functions, left for later phases
//Vanilla's async CompletableFuture scheduling is simplified here to synchronous SimpleReloadInstance.Run
public sealed class ReloadableServerResources
{
    //ResourceManager the resource pack aggregation entry; listeners share the same instance
    public ResourceManager ResourceManager { get; }
    //TagManager tag manager, exposed to the startup sequence through the BindAll entry
    public TagManager TagManager { get; }
    //Recipes recipe manager; crafting result queries and the recipe book both go through it
    public RecipeManager Recipes { get; }
    //Listeners the registered reload listeners, executed in registration order
    private readonly List<PreparableReloadListener> _listeners = new();
    public IReadOnlyList<PreparableReloadListener> Listeners => _listeners;

    private ReloadableServerResources(ResourceManager rm, TagManager tm, RecipeManager recipes)
    {
        ResourceManager = rm;
        TagManager = tm;
        Recipes = recipes;
    }

    //LoadResources builds the instance, registers built-in listeners and triggers the first reload
    //registryAccess is provided by BuiltInRegistries.CreateRegistryAccess for TagManager.BindAll
    //Must be called after BootstrapClass.BootStrap because BindAll requires the Registry to be frozen
    //extraListeners additional listeners; the client uses it to attach the language table, the server passes none
    public static ReloadableServerResources LoadResources(ResourceManager rm, RegistryAccess registryAccess,
        IEnumerable<PreparableReloadListener>? extraListeners = null)
    {
        //Log.Debug($"ReloadableServerResources.LoadResources enter");
        var tm = new TagManager();
        var recipes = new RecipeManager();
        var rsr = new ReloadableServerResources(rm, tm, recipes);
        //Tags must be bound first; recipe ingredients may use item tags and the set must be queryable at parse time
        rsr._listeners.Add(new TagsReloadListener(tm, registryAccess));
        rsr._listeners.Add(new RecipeReloadListener(recipes));
        //Add AdvancementListener/FunctionListener here in the future
        if (extraListeners is not null) rsr._listeners.AddRange(extraListeners);
        rsr.Reload();
        //The menu needs the recipe table to compute crafting results, so it is attached right after assembly
        RecipeManager.Active = recipes;
        //Loot tables are read after the reload finishes so the final pack list is already in place
        NetCraft.Game.World.Loot.LootTables.Active = NetCraft.Game.World.Loot.LootTables.Load(rm);
        //Log.Debug($"ReloadableServerResources.LoadResources exit");
        return rsr;
    }

    //Reload triggers all listeners to reload in order, synchronously
    //Used by the runtime /reload command or after pack additions/removals
    public void Reload()
    {
        Log.Debug($"ReloadableServerResources.Reload entry listenerCount={_listeners.Count}");
        SimpleReloadInstance.Run(ResourceManager, _listeners);
        //The fuel table expands by item tags, so it must be built after tags are bound and rebuilt after a tag reload
        FuelValues.Active = FuelValues.VanillaBurnTimes();
        Log.Debug($"ReloadableServerResources.Reload exit fuel item count={FuelValues.Active.FuelItemCount}");
    }

    //AttachFunctionLibrary attaches the function library and immediately reloads it with the current resources, maps to the function library reload section of vanilla loadResources
    //After attaching to the listener list /reload also reloads the function library
    public void AttachFunctionLibrary(ServerFunctionLibrary library)
    {
        _listeners.Add(library);
        library.Reload(ResourceManager, new ReloadContext("functions", _listeners.Count - 1, _listeners.Count));
    }
}
