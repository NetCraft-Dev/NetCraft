using NetCraft.Commands;
using NetCraft.Commands.Functions;
using NetCraft.Logging;
using NetCraft.Registry;
using NetCraft.Util.Profiling;

namespace NetCraft.Game.Server;

//ServerFunctionManager 函数管理器对应原版 net.minecraft.server.ServerFunctionManager
//持函数库 每拍执行 tick 标签函数 重载后首拍执行 load 标签
//函数执行走执行上下文与函数调用队列 对应原版 execute 的 CONTEXT 链路
public sealed class ServerFunctionManager
{
    private static readonly Identifier TickFunctionTag = Identifier.WithDefaultNamespace("tick");
    private static readonly Identifier LoadFunctionTag = Identifier.WithDefaultNamespace("load");

    private readonly MinecraftServer _server;
    private readonly ServerFunctionLibrary _library;
    private IReadOnlyList<CommandFunction<CommandSourceStack>> _ticking = [];
    private bool _postReload;

    public ServerFunctionManager(MinecraftServer server, ServerFunctionLibrary library)
    {
        _server = server;
        _library = library;
        PostReload(library);
    }

    //Dispatcher 命令分发器经命令管理器
    public CommandDispatcher<CommandSourceStack> Dispatcher => _server.Commands.Dispatcher;

    //Library 函数库
    public ServerFunctionLibrary Library => _library;

    //Tick 每拍执行 tick 与 load 标签函数对应原版 tick
    public void Tick()
    {
        if (!_server.TickRate.RunsNormally)
        {
            return;
        }
        if (_postReload)
        {
            _postReload = false;
            var functions = _library.GetTag(LoadFunctionTag);
            ExecuteTagFunctions(functions, LoadFunctionTag);
        }
        ExecuteTagFunctions(_ticking, TickFunctionTag);
    }

    private void ExecuteTagFunctions(IReadOnlyList<CommandFunction<CommandSourceStack>> functions, Identifier loadFunctionTag)
    {
        Profiler.Get().Push(() => loadFunctionTag.ToString());
        foreach (var function in functions)
        {
            Execute(function, GameLoopSender());
        }
        Profiler.Get().Pop();
    }

    //ReplaceLibrary 换库并重算 tick 标签对应原版 replaceLibrary
    public void ReplaceLibrary(ServerFunctionLibrary library)
    {
        PostReload(library);
    }

    private void PostReload(ServerFunctionLibrary library)
    {
        _ticking = library.GetTag(TickFunctionTag);
        _postReload = true;
    }

    //GameLoopSender 游戏循环命令源权限 gamemasters 输出抑制对应原版 getGameLoopSender
    public CommandSourceStack GameLoopSender()
        => ServerCommandSource.GameLoop(_server);

    //Get 按标识查函数
    public CommandFunction<CommandSourceStack>? Get(Identifier id) => _library.GetFunction(id);

    //GetTag 取函数标签
    public IReadOnlyList<CommandFunction<CommandSourceStack>> GetTag(Identifier id) => _library.GetTag(id);

    //Execute 执行一个函数对应原版 execute
    //宏函数不带参实例化失败按原版吞掉 其他异常告警
    public void Execute(CommandFunction<CommandSourceStack> functionIn, CommandSourceStack sender)
    {
        var profiler = Profiler.Get();
        profiler.Push(() => "function " + functionIn.Id);
        try
        {
            var function = functionIn.Instantiate(null, Dispatcher);
            _server.Commands.ExecuteInContext(sender,
                context => ExecutionContext<CommandSourceStack>.QueueInitialFunctionCall(
                    context, function, sender, CommandResultCallback.Empty));
        }
        catch (FunctionInstantiationException)
        {
        }
        catch (Exception e)
        {
            Log.Warning($"Failed to execute function {functionIn.Id} {e}");
        }
        finally
        {
            profiler.Pop();
        }
    }
}
