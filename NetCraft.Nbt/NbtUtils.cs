using System.Globalization;
using System.Text;
using NetCraft.Config;

namespace NetCraft.Nbt;

//NBT utility class. Mirrors vanilla net.minecraft.nbt.NbtUtils.
//Provides pure NBT helpers: Tag comparison, pretty printing, data version read/write and BlockState string packing.
//Methods that depend on the Block/StateHolder system (readBlockState/writeBlockState/writeFluidState),
//methods that depend on Component (toPrettyComponent),
//and methods that depend on SNBT parsing (structureToSnbt/snbtToStructure/packStructureTemplate/unpackStructureTemplate)
//are not ported yet and will be completed once the matching subsystems are ready.
public static class NbtUtils
{
    public const string SnbtDataTag = "data";

    private const char PropertiesStart = '{';
    private const char PropertiesEnd = '}';
    private const char KeyValueSeparator = ':';
    private const int Indent = 2;
    private const string ElementSeparator = ",";
    private const string ColonSeparator = ":";

    // MIME line separator (vanilla net.minecraft.util.Crypt.MIME_LINE_SEPARATOR)
    private const string MimeLineSeparator = "\r\n";

    //Recursively compare two NBT values for equality.
    //Mirrors vanilla NbtUtils.compareNbt(Tag, Tag, boolean).
    //expected: the Tag to match against (used as the pattern).
    //actual: the Tag being checked.
    //partialListMatches: when true a ListTag may match as a subset (order does not matter, every expected element must be present).
    public static bool CompareNbt(Tag? expected, Tag? actual, bool partialListMatches)
    {
        if (ReferenceEquals(expected, actual) || expected is null)
            return true;
        if (actual is null || expected.GetType() != actual.GetType())
            return false;

        if (expected is CompoundTag expectedCompound)
        {
            var actualCompound = (CompoundTag)actual;
            if (actualCompound.Count < expectedCompound.Count)
                return false;
            foreach (var (key, tag) in expectedCompound)
            {
                if (!CompareNbt(tag, actualCompound[key], partialListMatches))
                    return false;
            }
            return true;
        }

        if (expected is ListTag expectedList)
        {
            if (partialListMatches)
            {
                var actualList = (ListTag)actual;
                if (expectedList.IsEmpty)
                    return actualList.IsEmpty;
                if (actualList.Count < expectedList.Count)
                    return false;
                foreach (var expectedTag in expectedList)
                {
                    var found = false;
                    foreach (var actualTag in actualList)
                    {
                        if (CompareNbt(expectedTag, actualTag, partialListMatches))
                        {
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                        return false;
                }
                return true;
            }
        }

        return expected.Equals(actual);
    }

    //AreEqual is full deep equality, matching the recursive semantics of vanilla Tag.equals
    //Unlike CompareNbt it requires equal key and element counts and does no subset matching
    //The NBT path and /data rely on it to tell whether a write really changed anything
    public static bool AreEqual(Tag? a, Tag? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null || a.Id != b.Id) return false;

        switch (a)
        {
            case CompoundTag leftCompound:
                var rightCompound = (CompoundTag)b;
                if (leftCompound.Count != rightCompound.Count) return false;
                foreach (var (key, value) in leftCompound)
                {
                    if (!rightCompound.TryGetTag(key, out var other) || !AreEqual(value, other)) return false;
                }
                return true;

            case ListTag leftList:
                var rightList = (ListTag)b;
                if (leftList.Count != rightList.Count) return false;
                for (var i = 0; i < leftList.Count; i++)
                {
                    if (!AreEqual(leftList[i], rightList[i])) return false;
                }
                return true;

            case ByteArrayTag leftBytes:
                return leftBytes.Value.AsSpan().SequenceEqual(((ByteArrayTag)b).Value);

            case IntArrayTag leftInts:
                return leftInts.Value.AsSpan().SequenceEqual(((IntArrayTag)b).Value);

            case LongArrayTag leftLongs:
                return leftLongs.Value.AsSpan().SequenceEqual(((LongArrayTag)b).Value);

            default:
                return a.Equals(b);
        }
    }

    //Pretty-print NBT. Mirrors vanilla NbtUtils.prettyPrint(Tag, boolean).
    public static string PrettyPrint(Tag tag, bool withBinaryBlobs)
        => PrettyPrint(new StringBuilder(), tag, 0, withBinaryBlobs).ToString();

    //Pretty-print NBT into a StringBuilder. Mirrors vanilla NbtUtils.prettyPrint(StringBuilder, Tag, int, boolean).
    public static StringBuilder PrettyPrint(StringBuilder builder, Tag input, int indent, bool withBinaryBlobs)
    {
        ArgumentNullException.ThrowIfNull(input);
        switch (input)
        {
            case NumericTag numeric:
                return builder.Append(numeric);
            case EndTag:
                return builder;
            case ByteArrayTag byteArray:
                return PrettyPrintByteArray(builder, byteArray, indent, withBinaryBlobs);
            case ListTag listTag:
                return PrettyPrintList(builder, listTag, indent, withBinaryBlobs);
            case IntArrayTag intArray:
                return PrettyPrintIntArray(builder, intArray, indent, withBinaryBlobs);
            case CompoundTag compound:
                return PrettyPrintCompound(builder, compound, indent, withBinaryBlobs);
            case LongArrayTag longArray:
                return PrettyPrintLongArray(builder, longArray, indent, withBinaryBlobs);
            default:
                return builder.Append(input);
        }
    }

    private static StringBuilder PrettyPrintByteArray(StringBuilder builder, ByteArrayTag tag, int indent, bool withBinaryBlobs)
    {
        var array = tag.Value;
        IndentTo(builder, indent).Append("byte[").Append(array.Length).Append("] {\n");
        if (withBinaryBlobs)
        {
            IndentTo(builder, indent + 1);
            for (var i = 0; i < array.Length; i++)
            {
                if (i != 0) builder.Append(',');
                if (i % 16 == 0 && i / 16 > 0)
                {
                    builder.Append('\n');
                    if (i < array.Length) IndentTo(builder, indent + 1);
                }
                else if (i != 0)
                {
                    builder.Append(' ');
                }
                builder.Append(string.Format(CultureInfo.InvariantCulture, "0x{0:X2}", array[i] & 0xFF));
            }
        }
        else
        {
            IndentTo(builder, indent + 1).Append(" // Skipped, supply withBinaryBlobs true");
        }
        builder.Append('\n');
        return IndentTo(builder, indent).Append('}');
    }

    private static StringBuilder PrettyPrintList(StringBuilder builder, ListTag tag, int indent, bool withBinaryBlobs)
    {
        var size = tag.Count;
        IndentTo(builder, indent).Append("list[").Append(size).Append("] [");
        if (size != 0) builder.Append('\n');
        for (var i = 0; i < size; i++)
        {
            if (i != 0) builder.Append(",\n");
            IndentTo(builder, indent + 1);
            PrettyPrint(builder, tag[i], indent + 1, withBinaryBlobs);
        }
        if (size != 0) builder.Append('\n');
        return IndentTo(builder, indent).Append(']');
    }

    private static StringBuilder PrettyPrintIntArray(StringBuilder builder, IntArrayTag tag, int indent, bool withBinaryBlobs)
    {
        var array = tag.Value;
        var hexWidth = 0;
        foreach (var v in array)
            hexWidth = Math.Max(hexWidth, string.Format(CultureInfo.InvariantCulture, "{0:X}", v).Length);
        IndentTo(builder, indent).Append("int[").Append(array.Length).Append("] {\n");
        if (withBinaryBlobs)
        {
            IndentTo(builder, indent + 1);
            for (var i = 0; i < array.Length; i++)
            {
                if (i != 0) builder.Append(',');
                if (i % 16 == 0 && i / 16 > 0)
                {
                    builder.Append('\n');
                    if (i < array.Length) IndentTo(builder, indent + 1);
                }
                else if (i != 0)
                {
                    builder.Append(' ');
                }
                builder.Append(string.Format(CultureInfo.InvariantCulture, "0x{0:D" + hexWidth + "}", array[i]));
            }
        }
        else
        {
            IndentTo(builder, indent + 1).Append(" // Skipped, supply withBinaryBlobs true");
        }
        builder.Append('\n');
        return IndentTo(builder, indent).Append('}');
    }

    private static StringBuilder PrettyPrintCompound(StringBuilder builder, CompoundTag tag, int indent, bool withBinaryBlobs)
    {
        var keys = tag.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
        IndentTo(builder, indent).Append('{');
        if (builder.Length - LastIndexOf(builder, MimeLineSeparator) > 2 * (indent + 1))
        {
            builder.Append('\n');
            IndentTo(builder, indent + 1);
        }
        var paddingLength = keys.Count == 0 ? 0 : keys.Max(k => k.Length);
        var padding = new string(' ', paddingLength);
        for (var i = 0; i < keys.Count; i++)
        {
            if (i != 0) builder.Append(",\n");
            var key = keys[i];
            IndentTo(builder, indent + 1).Append('"').Append(key).Append('"')
                .Append(padding, 0, padding.Length - key.Length)
                .Append(": ");
            PrettyPrint(builder, tag[key]!, indent + 1, withBinaryBlobs);
        }
        if (keys.Count != 0) builder.Append('\n');
        return IndentTo(builder, indent).Append('}');
    }

    private static StringBuilder PrettyPrintLongArray(StringBuilder builder, LongArrayTag tag, int indent, bool withBinaryBlobs)
    {
        var array = tag.Value;
        var hexWidth = 0L;
        foreach (var v in array)
            hexWidth = Math.Max(hexWidth, string.Format(CultureInfo.InvariantCulture, "{0:X}", v).Length);
        IndentTo(builder, indent).Append("long[").Append(array.Length).Append("] {\n");
        if (withBinaryBlobs)
        {
            IndentTo(builder, indent + 1);
            for (var i = 0; i < array.Length; i++)
            {
                if (i != 0) builder.Append(',');
                if (i % 16 == 0 && i / 16 > 0)
                {
                    builder.Append('\n');
                    if (i < array.Length) IndentTo(builder, indent + 1);
                }
                else if (i != 0)
                {
                    builder.Append(' ');
                }
                builder.Append(string.Format(CultureInfo.InvariantCulture, "0x{0:D" + hexWidth + "}", array[i]));
            }
        }
        else
        {
            IndentTo(builder, indent + 1).Append(" // Skipped, supply withBinaryBlobs true");
        }
        builder.Append('\n');
        return IndentTo(builder, indent).Append('}');
    }

    //Pad the current line with spaces up to column (2 * indent). Mirrors vanilla NbtUtils.indent(int, StringBuilder).
    private static StringBuilder IndentTo(StringBuilder builder, int indent)
    {
        var index = LastIndexOf(builder, MimeLineSeparator) + 1;
        var len = builder.Length - index;
        for (var i = 0; i < (2 * indent) - len; i++)
            builder.Append(' ');
        return builder;
    }

    //StringBuilder has no LastIndexOf, so this wraps a string lookup.
    private static int LastIndexOf(StringBuilder builder, string value)
    {
        // Simplification: convert to a string and search. StringBuilder is usually small, so the cost is acceptable.
        return builder.ToString().LastIndexOf(value, StringComparison.Ordinal);
    }

    // ============ data version read/write ============

    //Add the current data version to a CompoundTag. Mirrors vanilla addCurrentDataVersion(CompoundTag).
    public static CompoundTag AddCurrentDataVersion(CompoundTag tag)
        => AddDataVersion(tag, SharedConstants.WorldDataVersion);

    //Add the given data version to a CompoundTag. Mirrors vanilla addDataVersion(CompoundTag, int).
    public static CompoundTag AddDataVersion(CompoundTag tag, int version)
    {
        tag.PutInt(SharedConstants.DataVersionTag, version);
        return tag;
    }

    //Read the data version from a CompoundTag, defaulting to -1. Mirrors vanilla getDataVersion(CompoundTag).
    public static int GetDataVersion(CompoundTag tag) => GetDataVersion(tag, -1);

    //Read the data version from a CompoundTag, returning the default when absent. Mirrors vanilla getDataVersion(CompoundTag, int).
    public static int GetDataVersion(CompoundTag tag, int @default)
    {
        if (!tag.Contains(SharedConstants.DataVersionTag))
            return @default;
        return tag.GetIntValue(SharedConstants.DataVersionTag);
    }

    // ============ BlockState string packing/unpacking (pure string handling, no Block system)============

    //Pack a BlockState CompoundTag into a string. Mirrors vanilla NbtUtils.packBlockState(CompoundTag).
    //Format: name{key:value,key:value}
    public static string PackBlockState(CompoundTag compound)
    {
        var builder = new StringBuilder(compound.GetStringValue("Name"));
        if (compound.GetCompound("Properties") is { } properties)
        {
            var keyValues = string.Join(ElementSeparator,
                properties
                    .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                    .Select(kv => kv.Key + KeyValueSeparator.ToString() + kv.Value!.AsString()));
            builder.Append(PropertiesStart).Append(keyValues).Append(PropertiesEnd);
        }
        return builder.ToString();
    }

    //Unpack a string into a BlockState CompoundTag. Mirrors vanilla NbtUtils.unpackBlockState(String).
    //Format: name{key:value,key:value}
    public static CompoundTag UnpackBlockState(string compound)
    {
        var tag = new CompoundTag();
        string name;
        var openIndex = compound.IndexOf(PropertiesStart);
        if (openIndex >= 0)
        {
            name = compound.Substring(0, openIndex);
            var properties = new CompoundTag();
            if (openIndex + 2 <= compound.Length)
            {
                var closeIndex = compound.IndexOf(PropertiesEnd, openIndex);
                var values = compound.Substring(openIndex + 1, closeIndex - openIndex - 1);
                foreach (var keyValue in values.Split(ElementSeparator))
                {
                    var parts = keyValue.Split(ColonSeparator.ToCharArray(), 2);
                    if (parts.Length == 2)
                    {
                        properties.PutString(parts[0], parts[1]);
                    }
                }
            }
            tag.Put("Properties", properties);
        }
        else
        {
            name = compound;
        }
        tag.PutString("Name", name);
        return tag;
    }
}

