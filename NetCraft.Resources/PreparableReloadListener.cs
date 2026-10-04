namespace NetCraft.Resources;

//PreparableReloadListener 资源重载监听器对应原版同名接口
//同步签名适配 NetCraft 启动期阻塞模型原版 CompletableFuture 异步调度暂不移植
//未来异步化时 SimpleReloadInstance 内部改 Task 即可listener 接口不变
public interface PreparableReloadListener
{
    //Reload 在 ResourceManager 内容变更后被 SimpleReloadInstance 调用
    //rm 当前 ResourceManager 快照 ctx 进度与名称上下文供日志
    void Reload(ResourceManager rm, ReloadContext ctx);
}

//ReloadContext 重载上下文携带 listener 名称与序号供进度报告
//对应原版 PreparationBarrier+SharedState 简化为同步场景所需的最小信息
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
