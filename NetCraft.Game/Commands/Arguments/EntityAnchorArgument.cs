using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Commands.Suggestion;
using NetCraft.Game.Commands;
using NetCraft.Game.Server;
using NetCraft.Primitives;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//EntityAnchorArgument 实体锚点参数对应原版 EntityAnchorArgument
//feet脚部eyes眼睛 决定tp facing实体时取目标哪个基准点
public sealed class EntityAnchorArgument : ArgumentType<EntityAnchorArgument.Anchor>
{
    private static readonly IReadOnlyList<string> ExamplesList = new[] { "eyes", "feet" };

    public static readonly DynamicCommandExceptionType ErrorInvalid =
        new(name => new TranslatableMessage("argument.anchor.invalid", name));

    public static EntityAnchorArgument EntityAnchor() => new();

    //Anchor 锚点 feet取脚位置 eyes在脚位置上抬眼高
    public enum Anchor
    {
        Feet,
        Eyes,
    }

    private static readonly IReadOnlyDictionary<string, Anchor> ByName = new Dictionary<string, Anchor>
    {
        ["feet"] = Anchor.Feet,
        ["eyes"] = Anchor.Eyes,
    };

    //玩家站立眼高 实体系统接入前按玩家常量处理
    private const float PlayerEyeHeight = 1.62f;

    public Anchor Parse(StringReader reader)
    {
        var start = reader.Cursor;
        var name = reader.ReadUnquotedString();
        if (!ByName.TryGetValue(name, out var anchor))
        {
            reader.SetCursor(start);
            throw ErrorInvalid.CreateWithContext(reader, name);
        }
        return anchor;
    }

    //GetAnchor 取解析结果
    public static Anchor GetAnchor(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<Anchor>(name);

    //Apply 求执行者按该锚点的世界坐标
    public static Vec3 Apply(Anchor anchor, ServerCommandSource source)
        => Apply(anchor, source.PlayerOrThrow);

    //Apply 求玩家按该锚点的世界坐标
    public static Vec3 Apply(Anchor anchor, ServerPlayer player)
    {
        var pos = player.Position;
        return anchor == Anchor.Eyes
            ? new Vec3(pos.X, pos.Y + PlayerEyeHeight, pos.Z)
            : pos;
    }

    public Task<Suggestions> ListSuggestions<S>(CommandContext<S> context, SuggestionsBuilder builder)
    {
        foreach (var name in ByName.Keys)
            builder.Add(name);
        return builder.BuildFuture();
    }

    public IReadOnlyList<string> Examples => ExamplesList;
}
