using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Exceptions;
using NetCraft.Nbt;
using NetCraft.Registry;
using NetCraft.Registry.State;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//BlockInput 方块参数解析结果对应原版 BlockInput
//Nbt 是可选方块实体数据 语法里跟在方块状态之后
public sealed record BlockInput(BlockState State, CompoundTag? Nbt);

//BlockStateArgument 方块状态参数对应原版 net.minecraft.commands.arguments.blocks.BlockStateArgument
//语法 namespace:path[属性=值,...]{方块实体nbt} 属性与nbt都可省略
//nbt 含空格时整段用双引号包裹 引号内按转义规则解析
public sealed class BlockStateArgument : ArgumentType<BlockInput>
{
    public static readonly DynamicCommandExceptionType ErrorUnknownBlock =
        new(id => new LiteralMessage($"未知方块 {id}"));

    public static readonly SimpleCommandExceptionType ErrorInvalidState =
        new(new LiteralMessage("方块状态格式非法"));

    public static readonly SimpleCommandExceptionType ErrorUnknownProperty =
        new(new LiteralMessage("方块不存在该属性"));

    public static readonly SimpleCommandExceptionType ErrorInvalidPropertyValue =
        new(new LiteralMessage("属性取值非法"));

    public static BlockStateArgument Block() => new();

    public BlockInput Parse(StringReader reader)
    {
        var text = ReadToken(reader);

        //状态部分截止到首个属性段或nbt段 之后按顺序各解析一次
        var stateEnd = text.Length;
        var bracket = text.IndexOf('[');
        var brace = text.IndexOf('{');
        if (bracket >= 0 && bracket < stateEnd) stateEnd = bracket;
        if (brace >= 0 && brace < stateEnd) stateEnd = brace;

        var stateText = text[..stateEnd];
        var identifier = stateText.Contains(':')
            ? Identifier.TryParse(stateText)
            : Identifier.TryParse("minecraft:" + stateText);
        //BLOCK 是带默认值的注册表 未知 id 取出来会是 air 必须先用 ContainsKey 拦住
        if (identifier is null || !BuiltInRegistries.BLOCK.ContainsKey(identifier.Value))
            throw ErrorUnknownBlock.Create(stateText);
        var state = BuiltInRegistries.BLOCK.GetValue(identifier.Value)!.DefaultBlockState;
        var cursor = stateEnd;
        if (cursor < text.Length && text[cursor] == '[')
        {
            var close = text.IndexOf(']', cursor);
            if (close < 0) throw ErrorInvalidState.Create();
            state = ApplyProperties(state, text[(cursor + 1)..close]);
            cursor = close + 1;
        }

        CompoundTag? nbt = null;
        if (cursor < text.Length)
        {
            if (text[cursor] != '{' || !text.EndsWith('}')) throw ErrorInvalidState.Create();
            nbt = TagParser<Tag>.ParseCompoundFully(text[cursor..]);
        }
        return new BlockInput(state, nbt);
    }

    //ApplyProperties 逐个匹配属性名并取值 名字或取值不在定义内直接报语法错误
    private static BlockState ApplyProperties(BlockState state, string text)
    {
        if (text.Length == 0) return state;
        foreach (var pair in text.Split(','))
        {
            var separator = pair.IndexOf('=');
            if (separator <= 0) throw ErrorInvalidState.Create();
            var name = pair[..separator];
            var value = pair[(separator + 1)..];

            PropertyBase? property = null;
            foreach (var candidate in state.GetProperties())
            {
                if (candidate.Name != name) continue;
                property = candidate;
                break;
            }
            if (property is null) throw ErrorUnknownProperty.Create();
            var parsed = property.GetValueForName(value) ?? throw ErrorInvalidPropertyValue.Create();
            state = state.SetValue(property, parsed);
        }
        return state;
    }

    //ReadToken 读整段方块参数 带引号走转义解析 否则读到空白为止
    private static string ReadToken(StringReader reader)
    {
        if (reader.CanRead() && StringReader.IsQuotedStringStart(reader.Peek()))
            return reader.ReadQuotedString();
        var start = reader.Cursor;
        while (reader.CanRead() && !char.IsWhiteSpace(reader.Peek()))
            reader.Skip();
        return reader.String[start..reader.Cursor];
    }

    public IReadOnlyList<string> Examples => new[] { "minecraft:stone", "grass_block[snowy=true]", "chest{}" };
}
