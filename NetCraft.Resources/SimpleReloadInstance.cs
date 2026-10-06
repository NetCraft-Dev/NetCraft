namespace NetCraft.Resources;

//SimpleReloadInstance, a synchronous reload scheduler, a trimmed-down version of the vanilla class of the same name
//Walks listeners in registration order and calls Reload, no async scheduling or progress aggregation
//Vanilla uses chained CompletableFuture scheduling + AtomicInteger progress tracking, not needed at startup
public static class SimpleReloadInstance
{
    //Run executes all listeners in order, a thrown exception aborts immediately and later listeners do not run
    public static void Run(ResourceManager rm, IReadOnlyList<PreparableReloadListener> listeners)
    {
        int total = listeners.Count;
        for (int i = 0; i < total; i++)
        {
            var ctx = new ReloadContext(listeners[i].GetType().Name, i, total);
            listeners[i].Reload(rm, ctx);
        }
    }
}
