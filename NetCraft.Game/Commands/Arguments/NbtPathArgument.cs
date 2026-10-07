using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Nbt;
using NetCraft.Util;
using StringReader = NetCraft.Commands.StringReader;
using UtilSyntaxException = NetCraft.Util.Parsing.Packrat.Commands.CommandSyntaxException;

namespace NetCraft.Game.Commands.Arguments;

//NbtPath NBT path, maps to vanilla net.minecraft.commands.arguments.NbtPathArgument.NbtPath
//A path is made of nodes: a.b child key, [0] index, [] all elements, {k=v} pattern match; the result is always a tag list
//This project treats ListTag as the collection; the three array tags are handled as vanilla "readable but not writable" (vanilla arrays throw on add/remove)
public sealed class NbtPath
{
    //MaxDepth path depth limit, maps to vanilla isTooDeep's 512
    private const int MaxDepth = 512;

    public static readonly SimpleCommandExceptionType ErrorInvalidNode =
        new(new LiteralMessage("invalid NBT path node"));

    public static readonly SimpleCommandExceptionType ErrorDataTooDeep =
        new(new LiteralMessage("NBT data nested too deeply"));

    public static readonly DynamicCommandExceptionType ErrorNothingFound =
        new(arg => new LiteralMessage($"no matching tag found on path {arg}"));

    public static readonly DynamicCommandExceptionType ErrorExpectedList =
        new(arg => new LiteralMessage($"the position is not a list: {arg}"));

    public static readonly DynamicCommandExceptionType ErrorInvalidIndex =
        new(arg => new LiteralMessage($"list index out of bounds: {arg}"));

    private readonly string _original;
    private readonly Node[] _nodes;
    //_nodeToOriginalPosition node to its end position in the original string; when reporting "tag not found" it truncates the path to the failing node
    private readonly Dictionary<Node, int> _nodeToOriginalPosition;

    private NbtPath(string original, Node[] nodes, Dictionary<Node, int> nodeToOriginalPosition)
    {
        _original = original;
        _nodes = nodes;
        _nodeToOriginalPosition = nodeToOriginalPosition;
    }

    //Original the original path string
    public string Original => _original;

    //AsString the original path string, maps to vanilla asString
    public string AsString() => _original;

    public override string ToString() => _original;

    //Of parses a whole path string, used by tests and internal construction
    public static NbtPath Of(string path)
    {
        var reader = new StringReader(path);
        var result = Parse(reader);
        if (reader.CanRead()) throw ErrorInvalidNode.Create();
        return result;
    }

    //Parse parses the path from the command reader, called by NbtPathArgument
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
            //Besides '.', a node can be followed directly by [ and {; vanilla likewise does not require the dot
            if (next != ' ' && next != '[' && next != '{') ExpectDot(reader);
        }
        if (nodes.Count == 0) throw ErrorInvalidNode.CreateWithContext(reader);
        return new NbtPath(reader.String[start..reader.Cursor], nodes.ToArray(), positions);
    }

    //Get resolves the value along the path; an empty result at any layer reports not-found, maps to vanilla get
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

    //CountMatching counts along the path; an empty result returns 0 without error, maps to vanilla countMatching
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

    //GetOrCreate resolves to the last layer; a missing intermediate layer is created with the next node's preferred type, maps to vanilla getOrCreate
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

    //Set writes the value into every position hit by the path and returns the number actually changed, maps to vanilla set
    //Vanilla copies once and each later position gets its own copy, so positions never share the same instance
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

    //Insert inserts a value into every list hit by the path; a negative index counts from the end like vanilla, maps to vanilla insert
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

    //Remove removes every position hit by the path and returns the count removed, maps to vanilla remove
    public int Remove(Tag tag)
    {
        var result = new List<Tag> { tag };
        for (var i = 0; i < _nodes.Length - 1; i++) result = _nodes[i].Get(result);
        var lastNode = _nodes[^1];
        var removed = 0;
        foreach (var target in result) removed += lastNode.RemoveTag(target);
        return removed;
    }

    //IsTooDeep recursively checks whether the nesting exceeds maxDepth, maps to vanilla isTooDeep
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

    // ============ parsing ============

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

    //ReadObjectNode a name directly followed by { means pattern matching, otherwise it is a plain child key
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

    //ParseCompound reads a compound tag with the SNBT parser; the SNBT parser has its own cursor and writes the position back to the command reader
    //SNBT exceptions are uniformly converted into path node exceptions
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

    //IsAllowedInUnquotedName characters allowed in an unquoted key name, maps to vanilla isAllowedInUnquotedName
    private static bool IsAllowedInUnquotedName(char c)
        => c is not (' ' or '"' or '\'' or '[' or ']' or '.' or '{' or '}');

    // ============ collection access ============

    //CollectionSize returns the element count; non-collections return -1
    private static int CollectionSize(Tag tag) => tag switch
    {
        ListTag list => list.Count,
        ByteArrayTag array => array.Length,
        IntArrayTag array => array.Length,
        LongArrayTag array => array.Length,
        _ => -1,
    };

    //CollectionElementAt returns the element; out of bounds or non-collection returns null
    //Array elements are temporary copies unrelated to the array itself, so arrays only support reading
    private static Tag? CollectionElementAt(Tag tag, int index) => tag switch
    {
        ListTag list => index >= 0 && index < list.Count ? list[index] : null,
        ByteArrayTag array => index >= 0 && index < array.Length ? new ByteTag(array.Value[index]) : null,
        IntArrayTag array => index >= 0 && index < array.Length ? new IntTag(array.Value[index]) : null,
        LongArrayTag array => index >= 0 && index < array.Length ? new LongTag(array.Value[index]) : null,
        _ => null,
    };

    //TagsEqual deep equality, used to tell whether a write actually changed the content; reuses the core implementation
    private static bool TagsEqual(Tag? a, Tag? b) => NbtUtils.AreEqual(a, b);

    // ============ nodes ============

    private abstract class Node
    {
        public abstract void GetTag(Tag parent, List<Tag> output);

        public abstract void GetOrCreateTag(Tag parent, Func<Tag> child, List<Tag> output);

        //CreatePreferredParentTag the type this node wants created when an intermediate layer is missing, maps to the vanilla same-named method
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

    //CompoundChildNode plain child key, the b in a.b
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

    //IndexedElementNode index node [0]; negatives count from the end
    private sealed class IndexedElementNode(int index) : Node
    {
        public override void GetTag(Tag parent, List<Tag> output)
        {
            var size = CollectionSize(parent);
            if (size < 0) return;
            var actualIndex = index < 0 ? size + index : index;
            if (CollectionElementAt(parent, actualIndex) is { } element) output.Add(element);
        }

        //Vanilla's node does not create list elements, it just carries existing elements out
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

    //AllElementsNode all-elements node [] hits every item in the list
    private sealed class AllElementsNode : Node
    {
        public static readonly AllElementsNode Instance = new();

        private AllElementsNode() { }

        public override void GetTag(Tag parent, List<Tag> output)
        {
            if (parent is not ListTag list) return;
            foreach (var tag in list) output.Add(tag);
        }

        //Adds a new element when the list is empty, otherwise carries all existing elements out, maps to vanilla getOrCreateTag
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

    //MatchElementNode list element pattern match [{k=v}], only acts on ListTag
    private sealed class MatchElementNode(CompoundTag pattern) : Node
    {
        public override void GetTag(Tag parent, List<Tag> output)
        {
            if (parent is not ListTag list) return;
            foreach (var tag in list)
                if (NbtUtils.CompareNbt(pattern, tag, true)) output.Add(tag);
        }

        //When nothing matches it adds a copy of the pattern and carries it out, maps to vanilla getOrCreateTag
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

    //MatchObjectNode child key pattern match a{k=v}; hits when the key exists and the content matches
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

    //MatchRootObjectNode root pattern match {k=v}, only valid at the first path position, hits the root itself
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

//NbtPathArgument NBT path argument, maps to vanilla net.minecraft.commands.arguments.NbtPathArgument
//Registered at network id 23 (nbt_path); the client tokenizes with the vanilla parser by the same id
public sealed class NbtPathArgument : ArgumentType<NbtPath>
{
    //Examples examples for suggestions and docs, matching vanilla EXAMPLES
    private static readonly IReadOnlyList<string> ExamplesList =
        new[] { "foo", "foo.bar", "foo[0]", "[0]", "[]", "{foo=bar}" };

    private static readonly NbtPathArgument Instance = new();

    //NbtPathArg path argument factory, maps to vanilla nbtPath; the name carries Arg to avoid a clash with the NbtPath type
    public static NbtPathArgument NbtPathArg() => Instance;

    public NbtPath Parse(StringReader reader) => NbtPath.Parse(reader);

    //GetPath gets the parsed path
    public static NbtPath GetPath(CommandContext<CommandSourceStack> context, string name)
        => context.GetArgument<NbtPath>(name);

    public IReadOnlyList<string> Examples => ExamplesList;
}
