using NetCraft.Codec;
using NetCraft.Game.Server;
using NetCraft.Registry;
using NetCraft.Registry.Codec;

namespace NetCraft.Game.World.Level.Timers;

//FunctionCallback 函数调用回调对应原版 net.minecraft.world.level.timers.FunctionCallback record
//到点后经函数管理器执行一次该函数
public sealed record FunctionCallback : TimerCallback<MinecraftServer>
{
    //Codec 单字段 id 对应原版 RecordCodecBuilder.mapCodec
    public static readonly MapCodec<TimerCallback<MinecraftServer>> Codec =
        RecordCodecBuilder.Of1<TimerCallback<MinecraftServer>, Identifier>(
            IdentifierCodec.Instance.FieldOf("id").ForGetter<TimerCallback<MinecraftServer>, Identifier>(c => ((FunctionCallback)c).FunctionId),
            id => new FunctionCallback(id));

    public Identifier FunctionId { get; }

    public FunctionCallback(Identifier functionId) => FunctionId = functionId;

    public override void Handle(MinecraftServer server, TimerQueue<MinecraftServer> queue, long time)
    {
        var functionManager = server.Functions;
        if (functionManager.Get(FunctionId) is { } function)
        {
            functionManager.Execute(function, functionManager.GameLoopSender());
        }
    }

    public override MapCodec<TimerCallback<MinecraftServer>> GetCodec() => Codec;

    public override string ToString() => $"FunctionCallback[{FunctionId}]";
}
