using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Game.Commands.Arguments;
using NetCraft.Registry;
using GameMobEffect = NetCraft.Game.World.Effect.MobEffect;
using GameMobEffectInstance = NetCraft.Game.World.Effect.MobEffectInstance;

namespace NetCraft.Game.Commands;

//EffectCommand effect 命令对应原版 net.minecraft.server.commands.EffectCommands
//clear 清除目标效果 give 施加效果 之后可跟时长/等级/是否隐藏粒子三段
public static class EffectCommand
{
    //ErrorGiveFailed 没有任何目标成功施加效果 对应原版 commands.effect.give.failed
    private static readonly SimpleCommandExceptionType ErrorGiveFailed =
        new(new LiteralMessage("该效果未能施加于任何目标"));

    //ErrorClearEverythingFailed 没有任何目标被清除全部效果 对应原版 commands.effect.clear.everything.failed
    private static readonly SimpleCommandExceptionType ErrorClearEverythingFailed =
        new(new LiteralMessage("未能清除任何效果"));

    //ErrorClearSpecificFailed 没有任何目标被清除指定效果 对应原版 commands.effect.clear.specific.failed
    private static readonly SimpleCommandExceptionType ErrorClearSpecificFailed =
        new(new LiteralMessage("未能清除该效果"));

    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        //clear [<targets>] [<effect>] 不传 targets 时作用于执行者自己
        var clearEffect = RequiredArgumentBuilder<CommandSourceStack, Identifier>.Argument("effect", EffectArgument())
            .Executes(c => ClearEffect(c, EntityArgument.GetEntities(c, "targets"), ResourceArgument.GetMobEffect(c, "effect")));
        var clearTargets = RequiredArgumentBuilder<CommandSourceStack, EntitySelector>.Argument("targets", EntityArgument.Entities())
            .Executes(c => ClearEffects(c, EntityArgument.GetEntities(c, "targets")))
            .Then(clearEffect);
        var clear = LiteralArgumentBuilder<CommandSourceStack>.Literal("clear")
            .Executes(c => ClearEffects(c, SelfTargets(c)))
            .Then(clearTargets);

        //give <targets> <effect> [<seconds>|infinite] [<amplifier>] [<hideParticles>]
        var hideParticles = RequiredArgumentBuilder<CommandSourceStack, bool>.Argument("hideParticles", BoolArgumentType.Bool())
            .Executes(c => GiveEffect(c, EntityArgument.GetEntities(c, "targets"), ResourceArgument.GetMobEffect(c, "effect"),
                IntegerArgumentType.GetInteger(c, "seconds"), IntegerArgumentType.GetInteger(c, "amplifier"),
                !BoolArgumentType.GetBool(c, "hideParticles")));
        var amplifier = RequiredArgumentBuilder<CommandSourceStack, int>.Argument("amplifier", IntegerArgumentType.Integer(0, 255))
            .Executes(c => GiveEffect(c, EntityArgument.GetEntities(c, "targets"), ResourceArgument.GetMobEffect(c, "effect"),
                IntegerArgumentType.GetInteger(c, "seconds"), IntegerArgumentType.GetInteger(c, "amplifier"), true))
            .Then(hideParticles);
        var seconds = RequiredArgumentBuilder<CommandSourceStack, int>.Argument("seconds", IntegerArgumentType.Integer(1, 1000000))
            .Executes(c => GiveEffect(c, EntityArgument.GetEntities(c, "targets"), ResourceArgument.GetMobEffect(c, "effect"),
                IntegerArgumentType.GetInteger(c, "seconds"), 0, true))
            .Then(amplifier);
        var infiniteHideParticles = RequiredArgumentBuilder<CommandSourceStack, bool>.Argument("hideParticles", BoolArgumentType.Bool())
            .Executes(c => GiveEffect(c, EntityArgument.GetEntities(c, "targets"), ResourceArgument.GetMobEffect(c, "effect"),
                -1, IntegerArgumentType.GetInteger(c, "amplifier"), !BoolArgumentType.GetBool(c, "hideParticles")));
        var infiniteAmplifier = RequiredArgumentBuilder<CommandSourceStack, int>.Argument("amplifier", IntegerArgumentType.Integer(0, 255))
            .Executes(c => GiveEffect(c, EntityArgument.GetEntities(c, "targets"), ResourceArgument.GetMobEffect(c, "effect"),
                -1, IntegerArgumentType.GetInteger(c, "amplifier"), true))
            .Then(infiniteHideParticles);
        var infinite = LiteralArgumentBuilder<CommandSourceStack>.Literal("infinite")
            .Executes(c => GiveEffect(c, EntityArgument.GetEntities(c, "targets"), ResourceArgument.GetMobEffect(c, "effect"), -1, 0, true))
            .Then(infiniteAmplifier);
        var effect = RequiredArgumentBuilder<CommandSourceStack, Identifier>.Argument("effect", EffectArgument())
            .Executes(c => GiveEffect(c, EntityArgument.GetEntities(c, "targets"), ResourceArgument.GetMobEffect(c, "effect"), null, 0, true))
            .Then(seconds)
            .Then(infinite);
        var give = LiteralArgumentBuilder<CommandSourceStack>.Literal("give")
            .Then(RequiredArgumentBuilder<CommandSourceStack, EntitySelector>.Argument("targets", EntityArgument.Entities())
                .Then(effect));

        dispatcher.Register(LiteralArgumentBuilder<CommandSourceStack>.Literal("effect")
            .Requires(s => s.HasPermission(2))
            .Then(clear)
            .Then(give));
    }

    //EffectArgument effect 参数挂 mob_effect 注册表
    private static ResourceArgument EffectArgument() => new(Registries.MOB_EFFECT.Identifier);

    //SelfTargets 无参数 clear 的目标是执行者自己 对应原版 getEntityOrException
    private static IReadOnlyList<CommandTarget> SelfTargets(CommandContext<CommandSourceStack> context)
    {
        var source = (ServerCommandSource)context.GetSource();
        return new[] { CommandTarget.OfPlayer(source.PlayerOrThrow) };
    }

    //GiveEffect 给每个玩家目标施加效果 对应原版 giveEffect
    private static int GiveEffect(CommandContext<CommandSourceStack> context, IReadOnlyList<CommandTarget> targets,
        Holder<NetCraft.Registry.MobEffect> effectHolder, int? seconds, int amplifier, bool particles)
    {
        var source = (ServerCommandSource)context.GetSource();
        //瞬时效果只生效一刻 普通效果按秒折算 600 刻为默认 20 秒 -1 表示无限
        var instantaneous = effectHolder.Value is GameMobEffect effect && effect.IsInstantaneous;
        var duration = seconds != null
            ? (instantaneous ? seconds.Value : (seconds == -1 ? -1 : seconds.Value * 20))
            : (instantaneous ? 1 : 600);
        var count = 0;
        foreach (var target in targets)
        {
            //关卡实体没有效果存储 只处理玩家目标
            if (target.Player is not { } player) continue;
            var instance = new GameMobEffectInstance(effectHolder, duration, amplifier, ambient: false, visible: particles);
            if (player.AddEffect(instance)) count++;
        }
        if (count == 0) throw ErrorGiveFailed.Create();
        var name = EffectName(effectHolder);
        if (targets.Count == 1)
            source.SendSuccess($"已将 {name} 效果施加于 {targets[0].Name}，时长 {duration / 20} 秒");
        else
            source.SendSuccess($"已将 {name} 效果施加于 {targets.Count} 个目标");
        return count;
    }

    //ClearEffects 清除目标全部效果 对应原版 clearEffects
    private static int ClearEffects(CommandContext<CommandSourceStack> context, IReadOnlyList<CommandTarget> targets)
    {
        var source = (ServerCommandSource)context.GetSource();
        var count = 0;
        foreach (var target in targets)
        {
            if (target.Player is not { } player) continue;
            if (player.RemoveAllEffects()) count++;
        }
        if (count == 0) throw ErrorClearEverythingFailed.Create();
        if (targets.Count == 1)
            source.SendSuccess($"已清除 {targets[0].Name} 的全部效果");
        else
            source.SendSuccess($"已清除 {targets.Count} 个目标的全部效果");
        return count;
    }

    //ClearEffect 清除目标指定效果 对应原版 clearEffect
    private static int ClearEffect(CommandContext<CommandSourceStack> context, IReadOnlyList<CommandTarget> targets,
        Holder<NetCraft.Registry.MobEffect> effectHolder)
    {
        var source = (ServerCommandSource)context.GetSource();
        var count = 0;
        foreach (var target in targets)
        {
            if (target.Player is not { } player) continue;
            if (player.RemoveEffect(effectHolder.Value)) count++;
        }
        if (count == 0) throw ErrorClearSpecificFailed.Create();
        var name = EffectName(effectHolder);
        if (targets.Count == 1)
            source.SendSuccess($"已清除 {targets[0].Name} 的 {name} 效果");
        else
            source.SendSuccess($"已清除 {targets.Count} 个目标的 {name} 效果");
        return count;
    }

    //EffectName 取效果的注册名短形式 用于回执
    private static string EffectName(Holder<NetCraft.Registry.MobEffect> effectHolder)
        => BuiltInRegistries.MOB_EFFECT.GetKey(effectHolder.Value)?.ToShortString() ?? "未知效果";
}
