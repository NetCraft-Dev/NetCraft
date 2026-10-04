using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Registry;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//ResourceArgument 注册表资源参数对应原版 ResourceArgument
//持有目标注册表key解析标识符 合法性由命令执行时查注册表
public sealed class ResourceArgument : ArgumentType<Identifier>
{
    private static readonly IReadOnlyList<string> ExamplesList = new[] { "foo", "foo:bar", "012" };

    public static readonly DynamicCommandExceptionType ErrorResourceNotFound =
        new(id => new LiteralMessage($"未知资源 {id}"));

    //RegistryKey 目标注册表标识 如 minecraft:world_clock
    public Identifier RegistryKey { get; }

    public ResourceArgument(Identifier registryKey)
    {
        RegistryKey = registryKey;
    }

    public Identifier Parse(StringReader reader) => IdentifierArgument.ReadIdentifier(reader);

    //GetResource 取解析出的资源标识
    public static Identifier GetResource(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<Identifier>(name);

    //GetClock 取 world_clock 注册表的 Holder 查不到按原版抛资源不存在
    public static Holder<WorldClock> GetClock(CommandContext<CommandSourceStack> context, string name)
    {
        var id = context.GetArgument<Identifier>(name);
        return BuiltInRegistries.WORLD_CLOCK.Get(id)
            ?? throw ErrorResourceNotFound.Create(id);
    }

    //GetTimeline 取 timeline 注册表的 Holder 查不到按原版抛资源不存在
    public static Holder<Timeline> GetTimeline(CommandContext<CommandSourceStack> context, string name)
    {
        var id = context.GetArgument<Identifier>(name);
        return BuiltInRegistries.TIMELINE.Get(id)
            ?? throw ErrorResourceNotFound.Create(id);
    }

    //GetBiome 取 biome 注册表的 Holder 查不到按原版抛资源不存在
    public static Holder<Biome> GetBiome(CommandContext<CommandSourceStack> context, string name)
    {
        var id = context.GetArgument<Identifier>(name);
        return BuiltInRegistries.BIOME.Get(id)
            ?? throw ErrorResourceNotFound.Create(id);
    }

    //GetMobEffect 取 mob_effect 注册表的 Holder 查不到按原版抛资源不存在
    public static Holder<NetCraft.Registry.MobEffect> GetMobEffect(CommandContext<CommandSourceStack> context, string name)
    {
        var id = context.GetArgument<Identifier>(name);
        return BuiltInRegistries.MOB_EFFECT.Get(id)
            ?? throw ErrorResourceNotFound.Create(id);
    }

    public IReadOnlyList<string> Examples => ExamplesList;
}
