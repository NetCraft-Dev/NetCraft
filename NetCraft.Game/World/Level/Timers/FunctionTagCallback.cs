using NetCraft.Codec;
using NetCraft.Game.Server;
using NetCraft.Registry;
using NetCraft.Registry.Codec;

namespace NetCraft.Game.World.Level.Timers;

//FunctionTagCallback function tag callback, maps to vanilla net.minecraft.world.level.timers.FunctionTagCallback record
//When due, executes all functions under the tag in order
public sealed record FunctionTagCallback : TimerCallback<MinecraftServer>
{
    //Codec single field id, maps to vanilla RecordCodecBuilder.mapCodec
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
