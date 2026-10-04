using System.Globalization;
using System.Text;
using NetCraft.Config;

namespace NetCraft.Nbt;

//NBT 工具类。对应原版 net.minecraft.nbt.NbtUtils。
//提供 Tag 比较、格式化打印、数据版本读写、BlockState 字符串打包等纯 NBT 工具方法。
//依赖 Block/StateHolder 系统的方法（readBlockState/writeBlockState/writeFluidState），
//依赖 Component 的方法（toPrettyComponent），
//依赖 SNBT 解析的方法（structureToSnbt/snbtToStructure/packStructureTemplate/unpackStructureTemplate）
//暂未翻译，待对应子系统就绪后补全。
public static class NbtUtils
{
    public const string SnbtDataTag = "data";

    private const char PropertiesStart = '{';
    private const char PropertiesEnd = '}';
    private const char KeyValueSeparator = ':';
    private const int Indent = 2;
    private const string ElementSeparator = ",";
    private const string ColonSeparator = ":";

    // MIME 行分隔符（原版 net.minecraft.util.Crypt.MIME_LINE_SEPARATOR）
    private const string MimeLineSeparator = "\r\n";

    //递归比较两个 NBT 是否相等。
    //对应原版 NbtUtils.compareNbt(Tag, Tag, boolean)。
    //expected: 期望的 Tag（用作模式）。
    //actual: 实际的 Tag。
    //partialListMatches: 若为 true，ListTag 子集匹配即可（actual 顺序无关，期望元素都存在）。
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

    //AreEqual 完整深度相等 对应原版 Tag.equals 的递归语义
    //与 CompareNbt 的差别是要求键数与元素数都相等 不做子集匹配
    //NBT 路径与 /data 判断"写入前后是否真的变了"都靠它
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

    //格式化打印 NBT。对应原版 NbtUtils.prettyPrint(Tag, boolean)。
    public static string PrettyPrint(Tag tag, bool withBinaryBlobs)
        => PrettyPrint(new StringBuilder(), tag, 0, withBinaryBlobs).ToString();

    //格式化打印 NBT 到 StringBuilder。对应原版 NbtUtils.prettyPrint(StringBuilder, Tag, int, boolean)。
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

    //在当前行末尾填充空格到 (2 * indent) 列。对应原版 NbtUtils.indent(int, StringBuilder)。
    private static StringBuilder IndentTo(StringBuilder builder, int indent)
    {
        var index = LastIndexOf(builder, MimeLineSeparator) + 1;
        var len = builder.Length - index;
        for (var i = 0; i < (2 * indent) - len; i++)
            builder.Append(' ');
        return builder;
    }

    //StringBuilder 不支持 LastIndexOf，封装为字符串查找。
    private static int LastIndexOf(StringBuilder builder, string value)
    {
        // 简化：转字符串查找。StringBuilder 通常较小，性能可接受。
        return builder.ToString().LastIndexOf(value, StringComparison.Ordinal);
    }

    // ============ 数据版本读写 ============

    //添加当前数据版本到 CompoundTag。对应原版 addCurrentDataVersion(CompoundTag)。
    public static CompoundTag AddCurrentDataVersion(CompoundTag tag)
        => AddDataVersion(tag, SharedConstants.WorldDataVersion);

    //添加指定数据版本到 CompoundTag。对应原版 addDataVersion(CompoundTag, int)。
    public static CompoundTag AddDataVersion(CompoundTag tag, int version)
    {
        tag.PutInt(SharedConstants.DataVersionTag, version);
        return tag;
    }

    //读取 CompoundTag 中的数据版本，默认 -1。对应原版 getDataVersion(CompoundTag)。
    public static int GetDataVersion(CompoundTag tag) => GetDataVersion(tag, -1);

    //读取 CompoundTag 中的数据版本，缺失返回默认值。对应原版 getDataVersion(CompoundTag, int)。
    public static int GetDataVersion(CompoundTag tag, int @default)
    {
        if (!tag.Contains(SharedConstants.DataVersionTag))
            return @default;
        return tag.GetIntValue(SharedConstants.DataVersionTag);
    }

    // ============ BlockState 字符串打包/解包（纯字符串处理，不依赖 Block 系统）============

    //将 BlockState CompoundTag 打包为字符串。对应原版 NbtUtils.packBlockState(CompoundTag)。
    //格式：name{key:value,key:value}
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

    //将字符串解包为 BlockState CompoundTag。对应原版 NbtUtils.unpackBlockState(String)。
    //格式：name{key:value,key:value}
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

