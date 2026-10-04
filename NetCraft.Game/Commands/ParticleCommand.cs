using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Game.Network.Protocol.Game;
using NetCraft.Game.Server;
using NetCraft.Game.World.Particle;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.Commands;

//ParticleCommand particle 命令对应原版 net.minecraft.server.commands.ParticleCommand
//参数树 <name> [<pos>] [<delta>] <speed> <count> [force|normal] [<viewers>] 与原版一致
public static class ParticleCommand
{
    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        //参数链自底向上装配 force 与 normal 是 count 之下互斥的两个分支
        var forceViewers = RequiredArgumentBuilder<CommandSourceStack, EntitySelector>
            .Argument("viewers", EntityArgument.Players())
            .Executes(c => SendWithinRange(c, true, EntityArgument.GetPlayers(c, "viewers")));
        var force = LiteralArgumentBuilder<CommandSourceStack>.Literal("force")
            .Executes(c => SendWithinRange(c, true, null))
            .Then(forceViewers);
        var normalViewers = RequiredArgumentBuilder<CommandSourceStack, EntitySelector>
            .Argument("viewers", EntityArgument.Players())
            .Executes(c => SendWithinRange(c, false, EntityArgument.GetPlayers(c, "viewers")));
        var normal = LiteralArgumentBuilder<CommandSourceStack>.Literal("normal")
            .Executes(c => SendWithinRange(c, false, null))
            .Then(normalViewers);
        var count = RequiredArgumentBuilder<CommandSourceStack, int>.Argument("count", IntegerArgumentType.Integer(0))
            .Executes(c => SendWithinRange(c, false, null))
            .Then(force)
            .Then(normal);
        var speed = RequiredArgumentBuilder<CommandSourceStack, float>.Argument("speed", FloatArgumentType.FloatArg(0f))
            .Then(count);
        var delta = RequiredArgumentBuilder<CommandSourceStack, Coordinates>.Argument("delta", Vec3Argument.Vec3(false))
            .Then(speed);
        var pos = RequiredArgumentBuilder<CommandSourceStack, Coordinates>.Argument("pos", Vec3Argument.Vec3())
            .Executes(c => SendParticles(c, Vec3Argument.GetVec3(c, "pos"), Vec3.Zero, 0f, 0, false, null))
            .Then(delta);
        var name = RequiredArgumentBuilder<CommandSourceStack, ParticleOptions>.Argument("name", ParticleArgument.Particle())
            .Executes(c => SendParticles(c, null, Vec3.Zero, 0f, 0, false, null))
            .Then(pos);
        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("particle")
            .Requires(s => s.HasPermission(2))
            .Then(name));
    }

    //SendWithinRange count 之后的所有分支都把 pos/delta/speed/count 写全了 统一从上下文取
    private static int SendWithinRange(CommandContext<CommandSourceStack> context, bool force, IReadOnlyList<ServerPlayer>? viewers)
        => SendParticles(context, Vec3Argument.GetVec3(context, "pos"), Vec3Argument.GetVec3(context, "delta"),
            FloatArgumentType.GetFloat(context, "speed"), IntegerArgumentType.GetInteger(context, "count"), force, viewers);

    //SendParticles 给目标玩家发粒子包 对应原版 Level.sendParticles 的玩家投递
    //pos 为 null 用命令源位置 viewers 为 null 发全部在线玩家
    private static int SendParticles(CommandContext<CommandSourceStack> context, Vec3? pos, Vec3 delta,
        float speed, int count, bool force, IReadOnlyList<ServerPlayer>? viewers)
    {
        if (context.GetSource() is not ServerCommandSource source) return 0;
        var particle = ParticleArgument.GetParticle(context, "name");
        var origin = pos ?? source.Position;
        var targets = viewers ?? source.Server.PlayerList.Players;
        if (targets.Count == 0)
        {
            source.SendFailure("没有可接收粒子的玩家");
            return 0;
        }
        var packet = new ClientboundLevelParticlesPacket(origin.X, origin.Y, origin.Z,
            (float)delta.X, (float)delta.Y, (float)delta.Z, speed, count, force, false, particle);
        foreach (var player in targets)
            player.Connection.Send(packet);
        source.SendSuccess($"已向 {targets.Count} 名玩家发送粒子 {BuiltInRegistries.PARTICLE_TYPE.GetKey(particle.Type)}");
        return targets.Count;
    }
}
