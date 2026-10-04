namespace NetCraft.Network.Chat;

using System.Text;
using NetCraft.Codec;
using NetCraft.Commands;
using NetCraft.DataFixer.Util;
using NetCraft.Network.Chat.Contents;

//文本组件接口对应原版net.minecraft.network.chat.Component
//继承IMessage和FormattedText承载样式化的多段文本内容
public interface Component : IMessage, FormattedText
{
    //样式对应原版getStyle
    Style Style { get; }

    //内容对应原版getContents
    ComponentContents Contents { get; }

    //同级兄弟组件对应原版getSiblings
    IReadOnlyList<Component> Siblings { get; }

    //尝试折叠为纯字符串对应原版tryCollapseToString
    //仅当内容为纯文本且无样式无兄弟时返回字符串否则返回null
    string? TryCollapseToString()
    {
        if (Contents is not PlainTextContents text) return null;
        if (Siblings.Count > 0 || !Style.IsEmpty) return null;
        return text.Text;
    }

    //浅拷贝对应原版plainCopy仅复制内容不含样式和兄弟
    MutableComponent PlainCopy() => MutableComponent.Create(Contents);

    //深拷贝对应原版copy复制内容和兄弟和样式
    MutableComponent Copy() => new MutableComponent(Contents, new List<Component>(Siblings), Style);

    //带样式消费者遍历对应原版visit(StyledContentConsumer,Style)
    //对齐原版先访问自身内容再递归访问兄弟
    new Optional<T> Visit<T>(FormattedText.StyledContentConsumer<T> output, Style parentStyle)
    {
        var selfStyle = Style.ApplyTo(parentStyle);
        var selfResult = Contents.Visit(output, selfStyle);
        if (selfResult.IsPresent) return selfResult;
        foreach (var sibling in Siblings)
        {
            var result = sibling.Visit(output, selfStyle);
            if (result.IsPresent) return result;
        }
        return Optional<T>.Empty();
    }

    //无样式消费者遍历对应原版visit(ContentConsumer)
    new Optional<T> Visit<T>(FormattedText.ContentConsumer<T> output)
    {
        var selfResult = Contents.Visit(output);
        if (selfResult.IsPresent) return selfResult;
        foreach (var sibling in Siblings)
        {
            var result = sibling.Visit(output);
            if (result.IsPresent) return result;
        }
        return Optional<T>.Empty();
    }

    //无样式消费者遍历IMessage.GetString通过此路径
    Optional<T> FormattedText.Visit<T>(FormattedText.ContentConsumer<T> output) => Visit(output);

    //带样式消费者遍历FormattedText.Visit委托到Component.Visit
    Optional<T> FormattedText.Visit<T>(FormattedText.StyledContentConsumer<T> output, Style parentStyle) => Visit(output, parentStyle);

    //IMessage.GetString显式实现委托到Component.GetString
    string IMessage.GetString() => GetString();

    //拼接字符串对应原版getString
    //显式声明覆盖IMessage.GetString和FormattedText.GetString消除多重继承二义性
    new string GetString()
    {
        var builder = new StringBuilder();
        Visit(contents =>
        {
            builder.Append(contents);
            return Optional<object>.Empty();
        });
        return builder.ToString();
    }

    //带长度限制的字符串拼接对应原版getString(int)
    string GetString(int limit)
    {
        var builder = new StringBuilder();
        Visit(contents =>
        {
            var remaining = limit - builder.Length;
            if (remaining <= 0) return Optional<object>.Empty();
            builder.Append(contents.Length <= remaining ? contents : contents[..remaining]);
            return Optional<object>.Empty();
        });
        return builder.ToString();
    }

    //展平为组件列表对应原版toFlatList(Style)
    List<Component> ToFlatList(Style rootStyle)
    {
        var result = new List<Component>();
        Visit((style, contents) =>
        {
            if (contents.Length > 0)
            {
                result.Add(Literal(contents).WithStyle(style));
            }
            return Optional<object>.Empty();
        }, rootStyle);
        return result;
    }

    List<Component> ToFlatList() => ToFlatList(Style.Empty);

    //判断是否包含另一组件对应原版contains
    bool Contains(Component other)
    {
        if (Equals(other)) return true;
        var flat = ToFlatList();
        var otherFlat = other.ToFlatList(Style);
        if (otherFlat.Count == 0 || flat.Count < otherFlat.Count) return false;
        for (var i = 0; i <= flat.Count - otherFlat.Count; i++)
        {
            var match = true;
            for (var j = 0; j < otherFlat.Count; j++)
            {
                if (!flat[i + j].Equals(otherFlat[j])) { match = false; break; }
            }
            if (match) return true;
        }
        return false;
    }

    //null转空组件对应原版nullToEmpty
    public static Component NullToEmpty(string? text) => text is null ? CommonComponents.Empty : Literal(text);

    //纯文本组件对应原版literal
    public static MutableComponent Literal(string text) => MutableComponent.Create(PlainTextContents.Create(text));

    //翻译组件对应原版translatable
    public static MutableComponent Translatable(string key) => MutableComponent.Create(new TranslatableContents(key, null, TranslatableContents.NoArgs));

    //带参数翻译组件对应原版translatable(String,Object[])
    public static MutableComponent Translatable(string key, params object[] args) => MutableComponent.Create(new TranslatableContents(key, null, args));

    //带回退文本翻译组件对应原版translatableWithFallback(String,String)
    public static MutableComponent TranslatableWithFallback(string key, string? fallback) => MutableComponent.Create(new TranslatableContents(key, fallback, TranslatableContents.NoArgs));

    //带回退文本和参数翻译组件对应原版translatableWithFallback(String,String,Object[])
    public static MutableComponent TranslatableWithFallback(string key, string? fallback, params object[] args) => MutableComponent.Create(new TranslatableContents(key, fallback, args));

    //空组件对应原版empty
    public static MutableComponent Empty() => MutableComponent.Create(PlainTextContents.Empty);

    //按键绑定组件对应原版keybind
    public static MutableComponent Keybind(string name) => MutableComponent.Create(new KeybindContents(name));
}
