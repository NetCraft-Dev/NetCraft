using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Game.Server;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//DimensionArgument 维度参数对应原版 net.minecraft.commands.arguments.DimensionArgument
//解析维度标识 执行时到服务端已建的维度表里取实例 取不到按原版抛未知维度
public sealed class DimensionArgument : ArgumentType<Identifier>
{
    private static readonly IReadOnlyList<string> ExamplesList =
        new[] { "overworld", "overworld:the_nether", "minecraft:the_end" };

    public static readonly DynamicCommandExceptionType ErrorUnknownDimension =
        new(id => new LiteralMessage($"未知维度 {id}"));

    public static DimensionArgument Dimension() => new();

    public Identifier Parse(StringReader reader) => IdentifierArgument.ReadIdentifier(reader);

    //GetDimension 取维度世界 对应原版 DimensionArgument.getDimension
    public static PersistentServerLevel GetDimension(CommandContext<CommandSourceStack> context, string name)
    {
        var id = context.GetArgument<Identifier>(name);
        if (context.GetSource() is not ServerCommandSource source) throw ErrorUnknownDimension.Create(id);
        var key = ResourceKey<NetCraft.Registry.Level>.Create(Registries.DIMENSION, id);
        return source.Server.GetLevel(key) ?? throw ErrorUnknownDimension.Create(id);
    }

    public IReadOnlyList<string> Examples => ExamplesList;
}
