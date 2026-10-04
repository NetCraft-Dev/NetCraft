using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Nbt;
using NetCraft.Util;
using StringReader = NetCraft.Commands.StringReader;
using UtilSyntaxException = NetCraft.Util.Parsing.Packrat.Commands.CommandSyntaxException;

namespace NetCraft.Game.Commands.Arguments;

//NbtPath NBT 路径对应原版 net.minecraft.commands.arguments.NbtPathArgument.NbtPath
//路径由若干节点组成 a.b 子键 [0] 索引 [] 全元素 {k=v} 模式匹配 结果始终是标签列表
//本作集合以 ListTag 为准 三种数组标签按原版"可读不可写"处理(原版数组的增删会抛不支持)
public sealed class NbtPath
{
    //MaxDepth 路径深度上限 对应原版 isTooDeep 的 512
    private const int MaxDepth = 512;

    public static readonly SimpleCommandExceptionType ErrorInvalidNode =
        new(new LiteralMessage("NBT 路径节点不合法"));

    public static readonly SimpleCommandExceptionType ErrorDataTooDeep =
        new(new LiteralMessage("NBT 数据嵌套过深"));

    public static readonly DynamicCommandExceptionType ErrorNothingFound =
        new(arg => new LiteralMessage($"路径 {arg} 上找不到对应的标签"));

    public static readonly DynamicCommandExceptionType ErrorExpectedList =
        new(arg => new LiteralMessage($"该位置不是列表: {arg}"));

    public static readonly DynamicCommandExceptionType ErrorInvalidIndex =
        new(arg => new LiteralMessage($"列表索引越界: {arg}"));

    private readonly string _original;
    private readonly Node[] _nodes;
    //_nodeToOriginalPosition 节点到它在原字符串里的结束位置 报"找不到标签"时把路径截到出错节点
    private readonly Dictionary<Node, int> _nodeToOriginalPosition;

    private NbtPath(string original, Node[] nodes, Dictionary<Node, int> nodeToOriginalPosition)
    {
        _original = original;
        _nodes = nodes;
        _nodeToOriginalPosition = nodeToOriginalPosition;
    }

    //Original 原始路径字符串
    public string Original => _original;

    //AsString 原始路径字符串对应原版 asString
    public string AsString() => _original;

    public override string ToString() => _original;

    //Of 解析整条路径字符串 供测试与内部构造使用
    public static NbtPath Of(string path)
    {
        var reader = new StringReader(path);
        var result = Parse(reader);
        if (reader.CanRead()) throw ErrorInvalidNode.Create();
        return result;
    }

    //Parse 按命令 reader 解析路径 供 NbtPathArgument 调用
    public static NbtPath Parse(StringReader reader)
    {
        var nodes = new List<Node>();
        var start = reader.Cursor;
        var positions = new Dictionary<Node, int>();
        var firstNode = true;
        while (reader.CanRead() && reader.Peek() != ' ')
        {
            var node = ParseNode(reader, firstNode);
            nodes.Add(node);
            positions[node] = reader.Cursor - start;
            firstNode = false;
            if (!reader.CanRead()) break;
            var next = reader.Peek();
            //节点之间除了 . 还可以直接跟 [ 与 { 原版同样不强制点号
            if (next != ' ' && next != '[' && next != '{') ExpectDot(reader);
        }
        if (nodes.Count == 0) throw ErrorInvalidNode.CreateWithContext(reader);
        return new NbtPath(reader.String[start..reader.Cursor], nodes.ToArray(), positions);
    }

    //Get 沿路径取值 任意一层取空即报找不到 对应原版 get
    public List<Tag> Get(Tag tag)
    {
        var result = new List<Tag> { tag };
        foreach (var node in _nodes)
        {
            result = node.Get(result);
            if (result.Count == 0) throw CreateNotFoundException(node);
        }
        return result;
    }

    //CountMatching 沿路径计数 取空返回 0 不报错 对应原版 countMatching
    public int CountMatching(Tag tag)
    {
        var result = new List<Tag> { tag };
        foreach (var node in _nodes)
        {
            result = node.Get(result);
            if (result.Count == 0) return 0;
        }
        return result.Count;
    }

    //GetOrCreate 取到最后一层 中间层缺失按下一节点的偏好类型补出来 对应原版 getOrCreate
    public List<Tag> GetOrCreate(Tag tag, Func<Tag> newTagValue)
    {
        var result = GetOrCreateParents(tag);
        return _nodes[^1].GetOrCreate(result, newTagValue);
    }

    private List<Tag> GetOrCreateParents(Tag tag)
    {
        var result = new List<Tag> { tag };
        for (var i = 0; i < _nodes.Length - 1; i++)
        {
            var next = _nodes[i + 1];
            result = _nodes[i].GetOrCreate(result, next.CreatePreferredParentTag);
            if (result.Count == 0) throw CreateNotFoundException(_nodes[i]);
        }
        return result;
    }

    //Set 把值写进路径命中的每个位置 返回真正发生改动的个数 对应原版 set
    //原版只复制一次 后续位置各复制一份 保证各位置互不共享同一实例
    public int Set(Tag tag, Tag toAdd)
    {
        if (IsTooDeep(toAdd, _nodes.Length)) throw ErrorDataTooDeep.Create();
        var firstCopy = toAdd.Copy();
        var targets = GetOrCreateParents(tag);
        if (targets.Count == 0) return 0;
        var usedFirstCopy = false;
        var lastNode = _nodes[^1];
        var changed = 0;
        foreach (var target in targets)
        {
            changed += lastNode.SetTag(target, () =>
            {
                if (!usedFirstCopy)
                {
                    usedFirstCopy = true;
                    return firstCopy;
                }
                return firstCopy.Copy();
            });
        }
        return changed;
    }

    //Insert 往路径命中的每个列表插入值 index 为负按原版从尾部算 对应原版 insert
    public int Insert(int index, Tag target, IReadOnlyList<Tag> toInsert)
    {
        var copies = new List<Tag>(toInsert.Count);
        foreach (var tag in toInsert)
        {
            var copy = tag.Copy();
            copies.Add(copy);
            if (IsTooDeep(copy, _nodes.Length)) throw ErrorDataTooDeep.Create();
        }
        var targets = GetOrCreate(target, () => new ListTag());
        var modifiedCount = 0;
        var usedFirst = false;
        foreach (var targetTag in targets)
        {
            if (targetTag is not ListTag targetList) throw ErrorExpectedList.Create(targetTag);
            var modified = false;
            var actualIndex = index < 0 ? targetList.Count + index + 1 : index;
            foreach (var sourceTag in copies)
            {
                if (actualIndex < 0 || actualIndex > targetList.Count)
                    throw ErrorInvalidIndex.Create(actualIndex);
                var tagCopy = usedFirst ? sourceTag.Copy() : sourceTag;
                if (targetList.TryInsert(actualIndex, tagCopy))
                {
                    actualIndex++;
                    modified = true;
                }
            }
            usedFirst = true;
            if (modified) modifiedCount++;
        }
        return modifiedCount;
    }

    //Remove 删除路径命中的每个位置 返回删除个数 对应原版 remove
    public int Remove(Tag tag)
    {
        var result = new List<Tag> { tag };
        for (var i = 0; i < _nodes.Length - 1; i++) result = _nodes[i].Get(result);
        var lastNode = _nodes[^1];
        var removed = 0;
        foreach (var target in result) removed += lastNode.RemoveTag(target);
        return removed;
    }

    //IsTooDeep 递归判定嵌套是否超过 maxDepth 对应原版 isTooDeep
    public static bool IsTooDeep(Tag tag, int depth)
    {
        if (depth >= MaxDepth) return true;
        switch (tag)
        {
            case CompoundTag compound:
                foreach (var (_, child) in compound)
                    if (IsTooDeep(child, depth + 1)) return true;
                return false;
            case ListTag list:
                foreach (var child in list)
                    if (IsTooDeep(child, depth + 1)) return true;
                return false;
            default:
                return false;
        }
    }

    private CommandSyntaxException CreateNotFoundException(Node node)
    {
        var index = _nodeToOriginalPosition.TryGetValue(node, out var value) ? value : _original.Length;
        return ErrorNothingFound.Create(_original[..Math.Min(index, _original.Length)]);
    }

    // ============ 解析 ============

    private static void ExpectDot(StringReader reader)
    {
        if (!reader.CanRead() || reader.Peek() != '.') throw ErrorInvalidNode.CreateWithContext(reader);
        reader.Skip();
    }

    private static Node ParseNode(StringReader reader, bool firstNode)
    {
        var next = reader.Peek();
        switch (next)
        {
            case '"':
            case '\'':
                return ReadObjectNode(reader, reader.ReadString());
            case '[':
                reader.Skip();
                if (!reader.CanRead()) throw ErrorInvalidNode.CreateWithContext(reader);
                var inner = reader.Peek();
                if (inner == '{')
                {
                    var elementPattern = ParseCompound(reader);
                    ExpectChar(reader, ']');
                    return new MatchElementNode(elementPattern);
                }
                if (inner == ']')
                {
                    reader.Skip();
                    return AllElementsNode.Instance;
                }
                var elementIndex = reader.ReadInt();
                ExpectChar(reader, ']');
                return new IndexedElementNode(elementIndex);
            case '{':
                if (!firstNode) throw ErrorInvalidNode.CreateWithContext(reader);
                return new MatchRootObjectNode(ParseCompound(reader));
            default:
                return ReadObjectNode(reader, ReadUnquotedName(reader));
        }
    }

    //ReadObjectNode 名称后面直接跟 { 表示按模式匹配 否则是普通子键
    private static Node ReadObjectNode(StringReader reader, string name)
    {
        if (name.Length == 0) throw ErrorInvalidNode.CreateWithContext(reader);
        if (reader.CanRead() && reader.Peek() == '{')
            return new MatchObjectNode(name, ParseCompound(reader));
        return new CompoundChildNode(name);
    }

    private static void ExpectChar(StringReader reader, char expected)
    {
        if (!reader.CanRead() || reader.Peek() != expected) throw ErrorInvalidNode.CreateWithContext(reader);
        reader.Skip();
    }

    //ParseCompound 借 SNBT 解析器读复合标签 SNBT 解析器有自己的游标 读完把位置写回命令 reader
    //SNBT 异常统一转成路径节点异常
    private static CompoundTag ParseCompound(StringReader reader)
    {
        var nbtReader = new CommandStringReader(reader.String) { Cursor = reader.Cursor };
        try
        {
            var tag = TagParser<Tag>.ParseCompoundAsArgument(nbtReader);
            reader.SetCursor(nbtReader.Cursor);
            return tag;
        }
        catch (UtilSyntaxException)
        {
            throw ErrorInvalidNode.CreateWithContext(reader);
        }
    }

    private static string ReadUnquotedName(StringReader reader)
    {
        var start = reader.Cursor;
        while (reader.CanRead() && IsAllowedInUnquotedName(reader.Peek())) reader.Skip();
        if (reader.Cursor == start) throw ErrorInvalidNode.CreateWithContext(reader);
        return reader.String[start..reader.Cursor];
    }

    //IsAllowedInUnquotedName 未加引号的键名允许的字符 对应原版 isAllowedInUnquotedName
    private static bool IsAllowedInUnquotedName(char c)
        => c is not (' ' or '"' or '\'' or '[' or ']' or '.' or '{' or '}');

    // ============ 集合访问 ============

    //CollectionSize 取集合元素数 非集合返回 -1
    private static int CollectionSize(Tag tag) => tag switch
    {
        ListTag list => list.Count,
        ByteArrayTag array => array.Length,
        IntArrayTag array => array.Length,
        LongArrayTag array => array.Length,
        _ => -1,
    };

    //CollectionElementAt 取集合元素 越界或非集合返回 null
    //数组元素是临时构造的副本 与数组本体无关联 故数组只支持读取
    private static Tag? CollectionElementAt(Tag tag, int index) => tag switch
    {
        ListTag list => index >= 0 && index < list.Count ? list[index] : null,
        ByteArrayTag array => index >= 0 && index < array.Length ? new ByteTag(array.Value[index]) : null,
        IntArrayTag array => index >= 0 && index < array.Length ? new IntTag(array.Value[index]) : null,
        LongArrayTag array => index >= 0 && index < array.Length ? new LongTag(array.Value[index]) : null,
        _ => null,
    };

    //TagsEqual 深度相等 判定写操作是否真的改变了内容 复用内核实现
    private static bool TagsEqual(Tag? a, Tag? b) => NbtUtils.AreEqual(a, b);

    // ============ 节点 ============

    private abstract class Node
    {
        public abstract void GetTag(Tag parent, List<Tag> output);

        public abstract void GetOrCreateTag(Tag parent, Func<Tag> child, List<Tag> output);

        //CreatePreferredParentTag 中间层缺失时该节点希望补出的类型 对应原版同名方法
        public abstract Tag CreatePreferredParentTag();

        public abstract int SetTag(Tag parent, Func<Tag> toAdd);

        public abstract int RemoveTag(Tag parent);

        public List<Tag> Get(List<Tag> tags)
        {
            var result = new List<Tag>();
            foreach (var tag in tags) GetTag(tag, result);
            return result;
        }

        public List<Tag> GetOrCreate(List<Tag> tags, Func<Tag> child)
        {
            var result = new List<Tag>();
            foreach (var tag in tags) GetOrCreateTag(tag, child, result);
            return result;
        }
    }

    //CompoundChildNode 普通子键 a.b 里的 b
    private sealed class CompoundChildNode(string name) : Node
    {
        public override void GetTag(Tag parent, List<Tag> output)
        {
            if (parent is CompoundTag compound && compound.TryGetTag(name, out var result)) output.Add(result);
        }

        public override void GetOrCreateTag(Tag parent, Func<Tag> child, List<Tag> output)
        {
            if (parent is not CompoundTag compound) return;
            if (!compound.TryGetTag(name, out var result))
            {
                result = child();
                compound.Put(name, result);
            }
            output.Add(result);
        }

        public override Tag CreatePreferredParentTag() => new CompoundTag();

        public override int SetTag(Tag parent, Func<Tag> toAdd)
        {
            if (parent is not CompoundTag compound) return 0;
            var newValue = toAdd();
            compound.TryGetTag(name, out var previousValue);
            compound.Put(name, newValue);
            return TagsEqual(newValue, previousValue) ? 0 : 1;
        }

        public override int RemoveTag(Tag parent)
        {
            if (parent is not CompoundTag compound || !compound.Contains(name)) return 0;
            compound.Remove(name);
            return 1;
        }
    }

    //IndexedElementNode 索引节点 [0] 负数从尾部算
    private sealed class IndexedElementNode(int index) : Node
    {
        public override void GetTag(Tag parent, List<Tag> output)
        {
            var size = CollectionSize(parent);
            if (size < 0) return;
            var actualIndex = index < 0 ? size + index : index;
            if (CollectionElementAt(parent, actualIndex) is { } element) output.Add(element);
        }

        //原版此节点不创建列表元素 只是把已有元素带出去
        public override void GetOrCreateTag(Tag parent, Func<Tag> child, List<Tag> output)
            => GetTag(parent, output);

        public override Tag CreatePreferredParentTag() => new ListTag();

        public override int SetTag(Tag parent, Func<Tag> toAdd)
        {
            if (parent is not ListTag list) return 0;
            var actualIndex = index < 0 ? list.Count + index : index;
            if (actualIndex < 0 || actualIndex >= list.Count) return 0;
            var previousValue = list[actualIndex];
            var newValue = toAdd();
            if (TagsEqual(newValue, previousValue)) return 0;
            return list.TrySet(actualIndex, newValue) ? 1 : 0;
        }

        public override int RemoveTag(Tag parent)
        {
            if (parent is not ListTag list) return 0;
            var actualIndex = index < 0 ? list.Count + index : index;
            if (actualIndex < 0 || actualIndex >= list.Count) return 0;
            return list.TryRemoveAt(actualIndex) ? 1 : 0;
        }
    }

    //AllElementsNode 全元素节点 [] 命中列表里每一项
    private sealed class AllElementsNode : Node
    {
        public static readonly AllElementsNode Instance = new();

        private AllElementsNode() { }

        public override void GetTag(Tag parent, List<Tag> output)
        {
            if (parent is not ListTag list) return;
            foreach (var tag in list) output.Add(tag);
        }

        //空列表时补一个新元素 否则把现有元素全部带出去 对应原版 getOrCreateTag
        public override void GetOrCreateTag(Tag parent, Func<Tag> child, List<Tag> output)
        {
            if (parent is not ListTag list) return;
            if (list.IsEmpty)
            {
                var result = child();
                if (list.TryInsert(0, result)) output.Add(result);
                return;
            }
            foreach (var tag in list) output.Add(tag);
        }

        public override Tag CreatePreferredParentTag() => new ListTag();

        public override int SetTag(Tag parent, Func<Tag> toAdd)
        {
            if (parent is not ListTag list) return 0;
            var size = list.Count;
            if (size == 0)
            {
                list.TryInsert(0, toAdd());
                return 1;
            }
            var newValue = toAdd();
            var unchanged = 0;
            foreach (var tag in list)
                if (TagsEqual(tag, newValue)) unchanged++;
            var changedCount = size - unchanged;
            if (changedCount == 0) return 0;
            list.Clear();
            if (!list.TryInsert(0, newValue)) return 0;
            for (var i = 1; i < size; i++) list.TryInsert(i, toAdd());
            return changedCount;
        }

        public override int RemoveTag(Tag parent)
        {
            if (parent is not ListTag list) return 0;
            var size = list.Count;
            if (size == 0) return 0;
            list.Clear();
            return size;
        }
    }

    //MatchElementNode 列表元素模式匹配 [{k=v}] 只作用于 ListTag
    private sealed class MatchElementNode(CompoundTag pattern) : Node
    {
        public override void GetTag(Tag parent, List<Tag> output)
        {
            if (parent is not ListTag list) return;
            foreach (var tag in list)
                if (NbtUtils.CompareNbt(pattern, tag, true)) output.Add(tag);
        }

        //一个都没匹配上就补一个模式副本并把它带出去 对应原版 getOrCreateTag
        public override void GetOrCreateTag(Tag parent, Func<Tag> child, List<Tag> output)
        {
            if (parent is not ListTag list) return;
            var found = false;
            foreach (var tag in list)
            {
                if (!NbtUtils.CompareNbt(pattern, tag, true)) continue;
                output.Add(tag);
                found = true;
            }
            if (found) return;
            var newTag = pattern.Copy();
            list.Add(newTag);
            output.Add(newTag);
        }

        public override Tag CreatePreferredParentTag() => new ListTag();

        public override int SetTag(Tag parent, Func<Tag> toAdd)
        {
            if (parent is not ListTag list) return 0;
            var size = list.Count;
            if (size == 0)
            {
                list.Add(toAdd());
                return 1;
            }
            var changedCount = 0;
            for (var i = 0; i < size; i++)
            {
                var currentValue = list[i];
                if (!NbtUtils.CompareNbt(pattern, currentValue, true)) continue;
                var newValue = toAdd();
                if (TagsEqual(newValue, currentValue)) continue;
                if (list.TrySet(i, newValue)) changedCount++;
            }
            return changedCount;
        }

        public override int RemoveTag(Tag parent)
        {
            if (parent is not ListTag list) return 0;
            var changedCount = 0;
            for (var i = list.Count - 1; i >= 0; i--)
            {
                if (!NbtUtils.CompareNbt(pattern, list[i], true)) continue;
                if (list.TryRemoveAt(i)) changedCount++;
            }
            return changedCount;
        }
    }

    //MatchObjectNode 子键模式匹配 a{k=v} 键存在且内容匹配才算命中
    private sealed class MatchObjectNode(string name, CompoundTag pattern) : Node
    {
        public override void GetTag(Tag parent, List<Tag> output)
        {
            if (parent is not CompoundTag compound) return;
            if (!compound.TryGetTag(name, out var result)) return;
            if (NbtUtils.CompareNbt(pattern, result, true)) output.Add(result);
        }

        public override void GetOrCreateTag(Tag parent, Func<Tag> child, List<Tag> output)
        {
            if (parent is not CompoundTag compound) return;
            if (!compound.TryGetTag(name, out var result))
            {
                var newTag = pattern.Copy();
                compound.Put(name, newTag);
                output.Add(newTag);
                return;
            }
            if (NbtUtils.CompareNbt(pattern, result, true)) output.Add(result);
        }

        public override Tag CreatePreferredParentTag() => new CompoundTag();

        public override int SetTag(Tag parent, Func<Tag> toAdd)
        {
            if (parent is not CompoundTag compound) return 0;
            if (!compound.TryGetTag(name, out var currentValue)) return 0;
            if (!NbtUtils.CompareNbt(pattern, currentValue, true)) return 0;
            var newValue = toAdd();
            if (TagsEqual(newValue, currentValue)) return 0;
            compound.Put(name, newValue);
            return 1;
        }

        public override int RemoveTag(Tag parent)
        {
            if (parent is not CompoundTag compound) return 0;
            if (!compound.TryGetTag(name, out var currentValue)) return 0;
            if (!NbtUtils.CompareNbt(pattern, currentValue, true)) return 0;
            compound.Remove(name);
            return 1;
        }
    }

    //MatchRootObjectNode 根模式匹配 {k=v} 只能出现在路径首位 命中根自身
    private sealed class MatchRootObjectNode(CompoundTag pattern) : Node
    {
        public override void GetTag(Tag parent, List<Tag> output)
        {
            if (parent is CompoundTag && NbtUtils.CompareNbt(pattern, parent, true)) output.Add(parent);
        }

        public override void GetOrCreateTag(Tag parent, Func<Tag> child, List<Tag> output)
            => GetTag(parent, output);

        public override Tag CreatePreferredParentTag() => new CompoundTag();

        public override int SetTag(Tag parent, Func<Tag> toAdd) => 0;

        public override int RemoveTag(Tag parent) => 0;
    }
}

//NbtPathArgument NBT 路径参数对应原版 net.minecraft.commands.arguments.NbtPathArgument
//注册在网络 id 23(nbt_path) 客户端按同 id 用原版解析器切词
public sealed class NbtPathArgument : ArgumentType<NbtPath>
{
    //Examples 补全与文档用的示例 与原版 EXAMPLES 一致
    private static readonly IReadOnlyList<string> ExamplesList =
        new[] { "foo", "foo.bar", "foo[0]", "[0]", "[]", "{foo=bar}" };

    private static readonly NbtPathArgument Instance = new();

    //NbtPathArg 路径参数工厂 对应原版 nbtPath 名字带 Arg 避开与 NbtPath 类型重名
    public static NbtPathArgument NbtPathArg() => Instance;

    public NbtPath Parse(StringReader reader) => NbtPath.Parse(reader);

    //GetPath 取解析出的路径
    public static NbtPath GetPath(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<NbtPath>(name);

    public IReadOnlyList<string> Examples => ExamplesList;
}
