using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Commands.Suggestion;
using NetCraft.Game.Server;
using NetCraft.Nbt;
using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Registry.State;
using NetCraft.Storage;
using StringReader = NetCraft.Commands.StringReader;

namespace NetCraft.Game.Commands.Arguments;

//BlockInWorld 谓词求值上下文对应原版 BlockInWorld
//方块实体不在 level 上 由调用方带上供 nbt 谓词使用
public sealed record BlockInWorld(PersistentServerLevel Level, BlockPos Pos, BlockEntityManager? BlockEntities)
{
    public BlockState? State => Level.GetBlockState(Pos);
}

//BlockPredicateArgument 方块谓词参数对应原版 net.minecraft.commands.arguments.blocks.BlockPredicateArgument
//语法 <方块> | #<标签> 之后可接 [属性=值,...] 与 {方块实体nbt}
//属性段只要求列出的属性匹配 没列出的忽略 nbt 段按部分匹配 给出的标签要落在实际标签里
public sealed class BlockPredicateArgument : ArgumentType<Predicate<BlockInWorld>>
{
    public static readonly DynamicCommandExceptionType ErrorUnknownBlock =
        new(id => new LiteralMessage($"未知方块 {id}"));

    public static readonly DynamicCommandExceptionType ErrorUnknownTag =
        new(id => new LiteralMessage($"未知方块标签 {id}"));

    public static readonly SimpleCommandExceptionType ErrorInvalidState =
        new(new LiteralMessage("方块谓词格式非法"));

    public static BlockPredicateArgument BlockPredicate() => new();

    public Predicate<BlockInWorld> Parse(StringReader reader)
    {
        var text = ReadToken(reader);

        //谓词头部截止到首个属性段或nbt段 之后按顺序各解析一次
        var headEnd = text.Length;
        var bracket = text.IndexOf('[');
        var brace = text.IndexOf('{');
        if (bracket >= 0 && bracket < headEnd) headEnd = bracket;
        if (brace >= 0 && brace < headEnd) headEnd = brace;
        var head = text[..headEnd];
        if (head.Length == 0) throw ErrorInvalidState.Create();

        var blockMatch = head[0] == '#' ? ParseTagMatch(head[1..]) : ParseBlockMatch(head);

        var cursor = headEnd;
        Func<BlockState, bool> propertyMatch = _ => true;
        if (cursor < text.Length && text[cursor] == '[')
        {
            var close = text.IndexOf(']', cursor);
            if (close < 0) throw ErrorInvalidState.Create();
            propertyMatch = ParsePropertyMatch(text[(cursor + 1)..close]);
            cursor = close + 1;
        }

        CompoundTag? nbtMatch = null;
        if (cursor < text.Length)
        {
            if (text[cursor] != '{' || !text.EndsWith('}')) throw ErrorInvalidState.Create();
            nbtMatch = TagParser<Tag>.ParseCompoundFully(text[cursor..]);
        }

        return world =>
        {
            var state = world.State;
            if (state is null) return false;
            if (!blockMatch(state.Value) || !propertyMatch(state.Value)) return false;
            if (nbtMatch is null) return true;
            var entity = world.BlockEntities?.Get(world.Pos);
            if (entity is null) return false;
            var tag = new CompoundTag();
            entity.SaveAdditional(tag);
            return MatchesTag(nbtMatch, tag);
        };
    }

    //ParseBlockMatch 按方块 id 匹配 未知 id 直接报错
    private static Func<BlockState, bool> ParseBlockMatch(string text)
    {
        var id = text.Contains(':') ? Identifier.TryParse(text) : Identifier.TryParse("minecraft:" + text);
        //BLOCK 是带默认值的注册表 未知 id 取出来会是 air 必须先用 ContainsKey 拦住
        if (id is null || !BuiltInRegistries.BLOCK.ContainsKey(id.Value))
            throw ErrorUnknownBlock.Create(text);
        var block = BuiltInRegistries.BLOCK.GetValue(id.Value)!;
        return state => ReferenceEquals(state.Owner, block);
    }

    //ParseTagMatch 按方块标签匹配 标签未注册直接报错
    private static Func<BlockState, bool> ParseTagMatch(string text)
    {
        var id = text.Contains(':') ? Identifier.TryParse(text) : Identifier.TryParse("minecraft:" + text);
        if (id is null) throw ErrorUnknownTag.Create(text);
        var key = TagKey<NetCraft.Registry.Block>.Create(Registries.BLOCK, id.Value);
        var set = BuiltInRegistries.BLOCK.Get(key);
        if (set is null) throw ErrorUnknownTag.Create(text);
        return state => set.IsBound && set.Any(holder => ReferenceEquals(holder.Value, state.Owner));
    }

    //ParsePropertyMatch 逐条匹配属性 带等号的要值相等 不带的只要求属性存在
    private static Func<BlockState, bool> ParsePropertyMatch(string text)
    {
        if (text.Length == 0) return _ => true;
        var conditions = new List<(string Name, string? Value)>();
        foreach (var pair in text.Split(','))
        {
            var separator = pair.IndexOf('=');
            if (separator < 0) conditions.Add((pair, null));
            else conditions.Add((pair[..separator], pair[(separator + 1)..]));
        }
        return state =>
        {
            foreach (var (name, value) in conditions)
            {
                var matched = false;
                foreach (var propertyValue in state.GetValues())
                {
                    if (propertyValue.Property.Name != name) continue;
                    matched = value is null || propertyValue.ValueName == value;
                    break;
                }
                if (!matched) return false;
            }
            return true;
        };
    }

    //MatchesTag 部分匹配 给出的每个条目都要在实际标签里存在且相等 对应原版 NbtPredicate
    //列表要求长度相等逐元素匹配 其余类型走相等比较
    private static bool MatchesTag(Tag expected, Tag actual)
    {
        if (expected is CompoundTag expectedCompound)
        {
            if (actual is not CompoundTag actualCompound) return false;
            foreach (var key in expectedCompound.Keys)
            {
                if (!actualCompound.TryGetTag(key, out var actualValue)) return false;
                if (!MatchesTag(expectedCompound.Get<Tag>(key)!, actualValue)) return false;
            }
            return true;
        }
        if (expected is ListTag expectedList)
        {
            if (actual is not ListTag actualList || actualList.Count != expectedList.Count) return false;
            for (var i = 0; i < expectedList.Count; i++)
            {
                if (!MatchesTag(expectedList[i], actualList[i])) return false;
            }
            return true;
        }
        return expected.Equals(actual);
    }

    //ReadToken 读整段谓词参数 带引号走转义解析 否则读到空白为止
    private static string ReadToken(StringReader reader)
    {
        if (reader.CanRead() && StringReader.IsQuotedStringStart(reader.Peek()))
            return reader.ReadQuotedString();
        var start = reader.Cursor;
        while (reader.CanRead() && !char.IsWhiteSpace(reader.Peek()))
            reader.Skip();
        return reader.String[start..reader.Cursor];
    }

    //GetBlockPredicate 取解析出的谓词
    public static Predicate<BlockInWorld> GetBlockPredicate(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<Predicate<BlockInWorld>>(name);

    //ListSuggestions 按当前游标给方块或标签候选
    public Task<Suggestions> ListSuggestions<S>(CommandContext<S> context, SuggestionsBuilder builder)
    {
        var remaining = builder.Remaining;
        if (remaining.StartsWith('#'))
        {
            foreach (var set in BuiltInRegistries.BLOCK.GetTags())
            {
                var text = "#" + set.Key.Location.ToShortString();
                if (text.StartsWith(remaining, StringComparison.Ordinal))
                    builder.Add(text);
            }
        }
        else
        {
            foreach (var id in BuiltInRegistries.BLOCK.KeySet)
            {
                var text = id.ToShortString();
                if (text.StartsWith(remaining, StringComparison.Ordinal))
                    builder.Add(text);
            }
            if ("#".StartsWith(remaining, StringComparison.Ordinal)) builder.Add("#");
        }
        return builder.BuildFuture();
    }

    public IReadOnlyList<string> Examples => new[] { "stone", "minecraft:stone[axis=y]", "#minecraft:logs" };
}
