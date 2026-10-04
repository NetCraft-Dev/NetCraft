namespace NetCraft.Resources;

//SimpleReloadInstance 同步重载调度器对应原版同名类精简版
//按注册顺序遍历 listener 调 Reload 不做异步调度与进度聚合
//原版用 CompletableFuture 链式调度+AtomicInteger 进度跟踪启动期不需要
public static class SimpleReloadInstance
{
    //Run 按顺序执行所有 listener 抛异常立即终止后续 listener 不执行
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
