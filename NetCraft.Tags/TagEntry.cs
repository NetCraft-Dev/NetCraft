using NetCraft.Codec;
using NetCraft.Registry;

namespace NetCraft.Tags;

//TagEntry 标签条目对应原版 net.minecraft.tags.TagEntry
//表示标签中的一个条目可以是元素引用或标签引用带 required 标志
//序列化格式对齐原版字符串前缀编码
//   #id 可选 tag    !#id 必选 tag    !id 必选 element    id 可选 element
public sealed record TagEntry(Identifier EntryId, bool Required, bool IsTag)
{
    //FromString 按前缀解析字符串为 TagEntry
    public static TagEntry FromString(string s)
    {
        if (s.StartsWith("!#"))
        {
            return new TagEntry(Identifier.Parse(s[2..]), true, true);
        }
        if (s.StartsWith("#"))
        {
            return new TagEntry(Identifier.Parse(s[1..]), false, true);
        }
        if (s.StartsWith("!"))
        {
            return new TagEntry(Identifier.Parse(s[1..]), true, false);
        }
        return new TagEntry(Identifier.Parse(s), false, false);
    }

    //AsString 序列化为前缀字符串
    public string AsString()
    {
        var tagPrefix = IsTag ? "#" : "";
        var requiredPrefix = Required ? "!" : "";
        return requiredPrefix + tagPrefix + EntryId;
    }

    //Element 工厂构造元素引用
    public static TagEntry Element(Identifier id, bool required) => new(id, required, false);

    //Tag 工厂构造标签引用
    public static TagEntry Tag(Identifier id, bool required) => new(id, required, true);

    //Build 把条目解析为元素加入 output 集合返回是否解析成功
    //elementGetter 元素引用解析回调失败时若 Required 返回 false 否则忽略
    //tagGetter 标签引用解析回调返回该标签的全部元素
    public bool Build<T>(
        Func<Identifier, Optional<T>> elementGetter,
        Func<Identifier, Optional<IEnumerable<T>>> tagGetter,
        ICollection<T> output)
    {
        if (!IsTag)
        {
            var value = elementGetter(EntryId);
            if (value.IsPresent)
            {
                output.Add(value.Get());
                return true;
            }
            return !Required;
        }
        var tagValues = tagGetter(EntryId);
        if (tagValues.IsPresent)
        {
            foreach (var v in tagValues.Get())
            {
                output.Add(v);
            }
            return true;
        }
        return !Required;
    }

    public override string ToString() => AsString();
}
