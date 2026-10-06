using NetCraft.Codec;
using NetCraft.Game.Server;
using NetCraft.Registry;
using NetCraft.Registry.Codec;

namespace NetCraft.Game.World.Level.Timers;

//FunctionTagCallback 函数标签回调对应原版 net.minecraft.world.level.timers.FunctionTagCallback record
//到点后按序执行标签下的全部函数
public sealed record FunctionTagCallback : TimerCallback<MinecraftServer>
{
    //Codec 单字段 id 对应原版 RecordCodecBuilder.mapCodec
    public static readonly MapCodec<TimerCallback<MinecraftServer>> Codec =
        RecordCodecBuilder.Of1<TimerCallback<MinecraftServer>, Identifier>(
            IdentifierCodec.Instance.FieldOf("id").ForGetter<TimerCallback<MinecraftServer>, Identifier>(c => ((FunctionTagCallback)c).TagId),
            id => new FunctionTagCallback(id));

    public Identifier TagId { get; }

    public FunctionTagCallback(Identifier tagId) => TagId = tagId;

    public override void Handle(MinecraftServer server, TimerQueue<MinecraftServer> queue, long time)
    {
        var functionManager = server.Functions;
        foreach (var function in functionManager.GetTag(TagId))
        {
            functionManager.Execute(function, functionManager.GameLoopSender());
        }
    }

    public override MapCodec<TimerCallback<MinecraftServer>> GetCodec() => Codec;

    public override string ToString() => $"FunctionTagCallback[{TagId}]";
}
