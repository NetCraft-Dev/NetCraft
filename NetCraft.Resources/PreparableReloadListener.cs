namespace NetCraft.Resources;

//PreparableReloadListener, resource reload listener, maps to the vanilla interface of the same name
//The synchronous signature fits NetCraft's blocking startup model, vanilla's CompletableFuture async scheduling is not ported for now
//When async is introduced later, only SimpleReloadInstance changes internally to Task, the listener interface stays the same
public interface PreparableReloadListener
{
    //Reload is called by SimpleReloadInstance after the ResourceManager content changes
    //rm is the current ResourceManager snapshot, ctx carries progress and name context for logs
    void Reload(ResourceManager rm, ReloadContext ctx);
}

//ReloadContext, reload context carrying the listener name and index for progress reports
//Vanilla's PreparationBarrier+SharedState reduced to the minimal information a synchronous scenario needs
public sealed class ReloadContext
{
    public string Name { get; }
    public int Index { get; }
    public int Total { get; }

    public ReloadContext(string name, int index, int total)
    {
        Name = name;
        Index = index;
        Total = total;
    }
}
