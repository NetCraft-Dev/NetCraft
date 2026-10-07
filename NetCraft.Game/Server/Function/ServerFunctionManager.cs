using NetCraft.Commands;
using NetCraft.Commands.Execution;
using NetCraft.Commands.Functions;
using NetCraft.Game.Commands;
using NetCraft.Logging;
using NetCraft.Registry;
using NetCraft.Util.Profiling;

namespace NetCraft.Game.Server;

//ServerFunctionManager function manager, maps to vanilla net.minecraft.server.ServerFunctionManager
//Holds the function library; runs tick tag functions every tick and the load tag the first tick after a reload
//Function execution goes through the execution context and the function call queue, maps to vanilla execute's CONTEXT chain
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

    //Dispatcher the command dispatcher, via the command manager
    public CommandDispatcher<CommandSourceStack> Dispatcher => _server.Commands.Dispatcher;

    //Library the function library
    public ServerFunctionLibrary Library => _library;

    //Tick runs the tick and load tag functions every tick, maps to vanilla tick
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

    //ReplaceLibrary swaps the library and recomputes the tick tag, maps to vanilla replaceLibrary
    public void ReplaceLibrary(ServerFunctionLibrary library)
    {
        PostReload(library);
    }

    private void PostReload(ServerFunctionLibrary library)
    {
        _ticking = library.GetTag(TickFunctionTag);
        _postReload = true;
    }

    //GameLoopSender the game loop command source with gamemasters permission and output suppressed, maps to vanilla getGameLoopSender
    public CommandSourceStack GameLoopSender()
        => ServerCommandSource.GameLoop(_server);

    //Get looks up a function by identifier
    public CommandFunction<CommandSourceStack>? Get(Identifier id) => _library.GetFunction(id);

    //GetTag gets a function tag
    public IReadOnlyList<CommandFunction<CommandSourceStack>> GetTag(Identifier id) => _library.GetTag(id);

    //Execute runs a function, maps to vanilla execute
    //A macro function that fails to instantiate without arguments is swallowed like vanilla; other exceptions are warned
    public void Execute(CommandFunction<CommandSourceStack> functionIn, CommandSourceStack sender)
    {
        var profiler = Profiler.Get();
        profiler.Push(() => "function " + functionIn.Id);
        try
        {
            var function = functionIn.Instantiate(null, Dispatcher);
            _server.Commands.ExecuteInContext(sender,
                context => NetCraft.Commands.Execution.ExecutionContext<CommandSourceStack>.QueueInitialFunctionCall(
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
