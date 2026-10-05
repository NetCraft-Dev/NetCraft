namespace NetCraft.Network.Chat;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using NetCraft.Codec;
using NetCraft.Nbt;
using NetCraft.Network.Chat.Contents;
using NetCraft.Network;

//Component序列化对应原版net.minecraft.network.chat.ComponentSerialization
//提供Component的JSON序列化和FriendlyByteBuf StreamCodec
//简化版支持纯文本/翻译/按键绑定/嵌套兄弟/基础样式
//复杂内容Score/Selector/Nbt/Object待业务类型补全后扩展
public static class ComponentSerialization
{
    //FriendlyByteBuf StreamCodec对应原版STREAM_CODEC
    //写JSON字符串到FriendlyByteBuf读JSON字符串解析为Component
    public static StreamCodec<FriendlyByteBuf, Component> StreamCodec { get; } = new ComponentStreamCodec();

    //Codec 组件持久化编解码 对应原版 CODEC
    //简化走 JSON 文本 解析失败返回错误而不是抛异常
    public static readonly Codec<Component> Codec = Codecs.String.ComapFlatMap(
        json =>
        {
            try
            {
                return DataResult<Component>.Success(FromJson(json));
            }
            catch (Exception e)
            {
                return DataResult<Component>.Error(() => e.Message);
            }
        },
        component => ToJson(component));

    //JSON序列化Component为字符串
    public static string ToJson(Component component)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        WriteComponent(writer, component);
        writer.Flush();
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    //JSON反序列化字符串为Component
    public static Component FromJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return ReadComponent(doc.RootElement);
    }

    //NBT标签还原组件 供命令参数把 SNBT 片段转成组件
    //对应原版 ComponentSerialization.CODEC 配 NbtOps 的组合
    public static Component FromTag(Tag tag) => FromJson(TagToJson(tag));

    //写入Component到JSON writer
    //纯文本无样式无兄弟优化为字符串否则写对象
    private static void WriteComponent(Utf8JsonWriter writer, Component component)
    {
        if (component.TryCollapseToString() is string text)
        {
            writer.WriteStringValue(text);
            return;
        }

        writer.WriteStartObject();
        WriteContents(writer, component.Contents);
        WriteStyle(writer, component.Style);
        if (component.Siblings.Count > 0)
        {
            writer.WritePropertyName("extra");
            writer.WriteStartArray();
            foreach (var sibling in component.Siblings)
            {
                WriteComponent(writer, sibling);
            }
            writer.WriteEndArray();
        }
        writer.WriteEndObject();
    }

    //写入内容字段根据ComponentContents类型分发
    private static void WriteContents(Utf8JsonWriter writer, ComponentContents contents)
    {
        switch (contents)
        {
            case PlainTextContents text:
                writer.WriteString("text", text.Text);
                break;
            case TranslatableContents translatable:
                writer.WriteString("translate", translatable.Key);
                if (translatable.Fallback is not null)
                    writer.WriteString("fallback", translatable.Fallback);
                if (translatable.Args.Length > 0)
                {
                    writer.WritePropertyName("with");
                    writer.WriteStartArray();
                    foreach (var arg in translatable.Args)
                        WriteArgument(writer, arg);
                    writer.WriteEndArray();
                }
                break;
            case KeybindContents keybind:
                writer.WriteString("keybind", keybind.Name);
                break;
            case ScoreContents score:
                writer.WriteStartObject("score");
                writer.WriteString("name", score.Name?.ToString());
                writer.WriteString("objective", score.Objective);
                writer.WriteEndObject();
                break;
            case SelectorContents selector:
                writer.WriteString("selector", selector.Pattern?.ToString());
                break;
            case NbtContents nbt:
                writer.WriteString("nbt", nbt.NbtPath?.ToString());
                if (nbt.Interpreting) writer.WriteBoolean("interpret", true);
                break;
        }
    }

    //写入翻译参数基础类型直接写Component递归
    private static void WriteArgument(Utf8JsonWriter writer, object arg)
    {
        if (arg is Component comp) WriteComponent(writer, comp);
        else if (arg is string s) writer.WriteStringValue(s);
        else if (arg is bool b) writer.WriteBooleanValue(b);
        else if (arg is int i) writer.WriteNumberValue(i);
        else if (arg is long l) writer.WriteNumberValue(l);
        else if (arg is float f) writer.WriteNumberValue(f);
        else if (arg is double d) writer.WriteNumberValue(d);
        else writer.WriteStringValue(arg?.ToString());
    }

    //写入样式非空字段才写
    private static void WriteStyle(Utf8JsonWriter writer, Style style)
    {
        if (style.Color is not null) writer.WriteString("color", style.Color.Serialize());
        if (style.IsBold) writer.WriteBoolean("bold", true);
        if (style.IsItalic) writer.WriteBoolean("italic", true);
        if (style.IsUnderlined) writer.WriteBoolean("underlined", true);
        if (style.IsStrikethrough) writer.WriteBoolean("strikethrough", true);
        if (style.IsObfuscated) writer.WriteBoolean("obfuscated", true);
        if (style.Insertion is not null) writer.WriteString("insertion", style.Insertion);
        if (!style.Font.Equals(FontDescription.Default)) writer.WriteString("font", style.Font.ToString());
    }

    //从JSON读取Component根据值类型分发
    private static Component ReadComponent(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return Component.Literal(element.GetString() ?? string.Empty);
            case JsonValueKind.Number:
                return Component.Literal(element.GetRawText());
            case JsonValueKind.Object:
                return ReadObjectComponent(element);
            case JsonValueKind.Array:
                return ReadArrayComponent(element);
            default:
                return Component.Empty();
        }
    }

    //从JSON数组读取首元素为内容其余为兄弟
    private static Component ReadArrayComponent(JsonElement element)
    {
        MutableComponent? result = null;
        foreach (var item in element.EnumerateArray())
        {
            var child = ReadComponent(item);
            if (result is null)
            {
                result = child.Copy();
            }
            else
            {
                result.Append(child);
            }
        }
        return result ?? Component.Empty();
    }

    //从JSON对象读取Component内容+样式+兄弟
    private static Component ReadObjectComponent(JsonElement element)
    {
        var contents = ReadContents(element);
        var component = MutableComponent.Create(contents);
        ReadStyle(element, component);
        if (element.TryGetProperty("extra", out var extraProp))
        {
            foreach (var item in extraProp.EnumerateArray())
                component.Append(ReadComponent(item));
        }
        return component;
    }

    //从JSON对象读取内容字段
    private static ComponentContents ReadContents(JsonElement element)
    {
        if (element.TryGetProperty("text", out var textProp))
            return PlainTextContents.Create(textProp.GetString() ?? string.Empty);

        if (element.TryGetProperty("translate", out var transProp))
        {
            var key = transProp.GetString() ?? string.Empty;
            var fallback = element.TryGetProperty("fallback", out var fbProp) ? fbProp.GetString() : null;
            var args = Array.Empty<object>();
            if (element.TryGetProperty("with", out var withProp))
                args = withProp.EnumerateArray().Select(ReadArgument).ToArray();
            return new TranslatableContents(key, fallback, args);
        }

        if (element.TryGetProperty("keybind", out var keyProp))
            return new KeybindContents(keyProp.GetString() ?? string.Empty);

        if (element.TryGetProperty("score", out var scoreProp))
        {
            var name = scoreProp.TryGetProperty("name", out var nProp) ? nProp.GetString() : null;
            var objective = scoreProp.TryGetProperty("objective", out var oProp) ? oProp.GetString() ?? string.Empty : string.Empty;
            return new ScoreContents(name ?? string.Empty, objective);
        }

        if (element.TryGetProperty("selector", out var selProp))
            return new SelectorContents(selProp.GetString() ?? string.Empty, null);

        if (element.TryGetProperty("nbt", out var nbtProp))
        {
            var interpret = element.TryGetProperty("interpret", out var iProp) && iProp.GetBoolean();
            return new NbtContents(nbtProp.GetString() ?? string.Empty, interpret, false, null, null!);
        }

        return PlainTextContents.Empty;
    }

    //从JSON读取翻译参数
    private static object ReadArgument(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String) return element.GetString() ?? string.Empty;
        if (element.ValueKind == JsonValueKind.Number)
        {
            if (element.TryGetInt32(out var i)) return i;
            if (element.TryGetInt64(out var l)) return l;
            return element.GetDouble();
        }
        if (element.ValueKind == JsonValueKind.True) return true;
        if (element.ValueKind == JsonValueKind.False) return false;
        if (element.ValueKind == JsonValueKind.Object) return ReadComponent(element);
        return element.ToString();
    }

    //从JSON读取样式应用到MutableComponent
    private static void ReadStyle(JsonElement element, MutableComponent component)
    {
        var style = Style.Empty;
        if (element.TryGetProperty("color", out var colorProp) && colorProp.GetString() is { } colorStr)
        {
            if (TextColor.ParseColor(colorStr) is { } color)
                style = style.WithColor(color);
        }
        if (TryGetBool(element, "bold", out var bold)) style = style.WithBold(bold);
        if (TryGetBool(element, "italic", out var italic)) style = style.WithItalic(italic);
        if (TryGetBool(element, "underlined", out var ul)) style = style.WithUnderlined(ul);
        if (TryGetBool(element, "strikethrough", out var st)) style = style.WithStrikethrough(st);
        if (TryGetBool(element, "obfuscated", out var ob)) style = style.WithObfuscated(ob);
        if (element.TryGetProperty("insertion", out var insProp) && insProp.GetString() is { } insertion)
            style = style.WithInsertion(insertion);
        component.SetStyle(style);
    }

    //读取可选布尔字段
    private static bool TryGetBool(JsonElement element, string name, out bool value)
    {
        value = false;
        if (element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.True)
        {
            value = true;
            return true;
        }
        return false;
    }

    //FriendlyByteBuf StreamCodec实现
    //26.2网络格式为无根名NBT对应原版ByteBufCodecs.fromCodecWithRegistries(tagCodec+NbtOps)
    //Component经JSON中转纯文本为StringTag其余为CompoundTag
    private sealed class ComponentStreamCodec : StreamCodec<FriendlyByteBuf, Component>
    {
        public Component Decode(FriendlyByteBuf buf)
        {
            var tag = buf.ReadNbt();
            return FromJson(TagToJson(tag));
        }

        public void Encode(FriendlyByteBuf buf, Component value)
        {
            using var doc = JsonDocument.Parse(ToJson(value));
            buf.WriteNbt(JsonToTag(doc.RootElement));
        }
    }

    //JsonElement转NBT Tag bool转ByteTag数字按精度选Int/Long/Double
    //数组混合元素类型由ListTag写出时统一包装为单字段"" CompoundTag
    private static Tag JsonToTag(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                return StringTag.ValueOf(element.GetString() ?? string.Empty);
            case JsonValueKind.True:
            case JsonValueKind.False:
                return new ByteTag((byte)(element.GetBoolean() ? 1 : 0));
            case JsonValueKind.Number:
                if (element.TryGetInt32(out var i)) return new IntTag(i);
                if (element.TryGetInt64(out var l)) return new LongTag(l);
                return new DoubleTag(element.GetDouble());
            case JsonValueKind.Object:
                var compound = new CompoundTag();
                foreach (var prop in element.EnumerateObject())
                    compound.Put(prop.Name, JsonToTag(prop.Value));
                return compound;
            case JsonValueKind.Array:
                var list = new ListTag();
                foreach (var item in element.EnumerateArray())
                    list.Add(JsonToTag(item));
                return list;
            default:
                return StringTag.ValueOf(string.Empty);
        }
    }

    //NBT Tag转JSON字符串 ByteTag还原为bool数值tag按原类型还原
    private static string TagToJson(Tag tag)
        => TagToJsonNode(tag)?.ToJsonString() ?? "null";

    private static JsonNode? TagToJsonNode(Tag tag)
    {
        switch (tag)
        {
            case StringTag s:
                return s.Value;
            case ByteTag b:
                return b.Value != 0;
            case ShortTag sh:
                return sh.Value;
            case IntTag i:
                return i.Value;
            case LongTag l:
                return l.Value;
            case FloatTag f:
                return f.Value;
            case DoubleTag d:
                return d.Value;
            case CompoundTag compound:
                var obj = new JsonObject();
                foreach (var (key, value) in compound)
                    obj[key] = TagToJsonNode(value);
                return obj;
            case ListTag list:
                var arr = new JsonArray();
                foreach (var item in list)
                    arr.Add(TagToJsonNode(item));
                return arr;
            case ByteArrayTag bytes:
                var byteArray = new JsonArray();
                foreach (var v in bytes.Value) byteArray.Add(v);
                return byteArray;
            case IntArrayTag ints:
                var intArray = new JsonArray();
                foreach (var v in ints.Value) intArray.Add(v);
                return intArray;
            case LongArrayTag longs:
                var longArray = new JsonArray();
                foreach (var v in longs.Value) longArray.Add(v);
                return longArray;
            default:
                return null;
        }
    }
}
