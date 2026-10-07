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

//EffectCommand effect command, maps to vanilla net.minecraft.server.commands.EffectCommands
//clear removes target effects; give applies an effect, optionally followed by duration/level/hide-particles
public static class EffectCommand
{
    //ErrorGiveFailed no target had the effect applied, maps to vanilla commands.effect.give.failed
    private static readonly SimpleCommandExceptionType ErrorGiveFailed =
        new(new LiteralMessage("the effect could not be applied to any target"));

    //ErrorClearEverythingFailed no target had all effects cleared, maps to vanilla commands.effect.clear.everything.failed
    private static readonly SimpleCommandExceptionType ErrorClearEverythingFailed =
        new(new LiteralMessage("could not clear any effects"));

    //ErrorClearSpecificFailed no target had the given effect cleared, maps to vanilla commands.effect.clear.specific.failed
    private static readonly SimpleCommandExceptionType ErrorClearSpecificFailed =
        new(new LiteralMessage("could not clear that effect"));

    public static void Register(CommandDispatcher<CommandSourceStack> dispatcher)
    {
        //clear [<targets>] [<effect>]; without targets it applies to the executor
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

    //EffectArgument effect argument bound to the mob_effect registry
    private static ResourceArgument EffectArgument() => new(Registries.MOB_EFFECT.Identifier);

    //SelfTargets the target of a parameterless clear is the executor, maps to vanilla getEntityOrException
    private static IReadOnlyList<CommandTarget> SelfTargets(CommandContext<CommandSourceStack> context)
    {
        var source = (ServerCommandSource)context.GetSource();
        return new[] { CommandTarget.OfPlayer(source.PlayerOrThrow) };
    }

    //GiveEffect applies the effect to each player target, maps to vanilla giveEffect
    private static int GiveEffect(CommandContext<CommandSourceStack> context, IReadOnlyList<CommandTarget> targets,
        Holder<NetCraft.Registry.MobEffect> effectHolder, int? seconds, int amplifier, bool particles)
    {
        var source = (ServerCommandSource)context.GetSource();
        //An instantaneous effect lasts one tick; a normal effect is converted to seconds, 600 ticks is the default 20 seconds, -1 means infinite
        var instantaneous = effectHolder.Value is GameMobEffect effect && effect.IsInstantaneous;
        var duration = seconds != null
            ? (instantaneous ? seconds.Value : (seconds == -1 ? -1 : seconds.Value * 20))
            : (instantaneous ? 1 : 600);
        var count = 0;
        foreach (var target in targets)
        {
            //Level entities have no effect storage; only player targets are handled
            if (target.Player is not { } player) continue;
            var instance = new GameMobEffectInstance(effectHolder, duration, amplifier, ambient: false, visible: particles);
            if (player.AddEffect(instance)) count++;
        }
        if (count == 0) throw ErrorGiveFailed.Create();
        var name = EffectName(effectHolder);
        if (targets.Count == 1)
            source.SendSuccess($"applied {name} to {targets[0].Name} for {duration / 20} seconds");
        else
            source.SendSuccess($"applied {name} to {targets.Count} targets");
        return count;
    }

    //ClearEffects clears all effects on the targets, maps to vanilla clearEffects
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
            source.SendSuccess($"cleared all effects on {targets[0].Name}");
        else
            source.SendSuccess($"cleared all effects on {targets.Count} targets");
        return count;
    }

    //ClearEffect clears the given effect on the targets, maps to vanilla clearEffect
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
            source.SendSuccess($"cleared {name} from {targets[0].Name}");
        else
            source.SendSuccess($"cleared {name} from {targets.Count} targets");
        return count;
    }

    //EffectName gets the effect's short registry name, for the reply
    private static string EffectName(Holder<NetCraft.Registry.MobEffect> effectHolder)
        => BuiltInRegistries.MOB_EFFECT.GetKey(effectHolder.Value)?.ToShortString() ?? "unknown effect";
}
