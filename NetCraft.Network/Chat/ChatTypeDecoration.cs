namespace NetCraft.Network.Chat;

//ChatTypeDecoration 聊天类型装饰对应原版 net.minecraft.network.chat.ChatTypeDecoration
//translationKey + parameters + style 三元组用于 Bound.decorate 生成翻译组件
//Parameter 枚举对齐原版 SENDER/TARGET/CONTENT VarInt id 编解码
public sealed class ChatTypeDecoration
{
    public string TranslationKey { get; }
    public List<Parameter> Parameters { get; }
    public Style Style { get; }

    public static StreamCodec<RegistryFriendlyByteBuf, ChatTypeDecoration> StreamCodec { get; }
        = new ChatTypeDecorationCodec();

    public ChatTypeDecoration(string translationKey, List<Parameter> parameters, Style style)
    {
        TranslationKey = translationKey;
        Parameters = parameters;
        Style = style;
    }

    //WithSender 仅 SENDER+CONTENT 参数对齐原版 withSender
    public static ChatTypeDecoration WithSender(string translationKey)
        => new(translationKey, new List<Parameter> { Parameter.SENDER, Parameter.CONTENT }, Style.Empty);

    //IncomingDirectMessage 灰色斜体 TARGET+CONTENT 对齐原版 incomingDirectMessage
    public static ChatTypeDecoration IncomingDirectMessage(string translationKey)
        => new(translationKey,
            new List<Parameter> { Parameter.SENDER, Parameter.CONTENT },
            Style.Empty.WithColor(0x808080));

    //OutgoingDirectMessage 灰色斜体 TARGET+CONTENT 对齐原版 outgoingDirectMessage
    public static ChatTypeDecoration OutgoingDirectMessage(string translationKey)
        => new(translationKey,
            new List<Parameter> { Parameter.TARGET, Parameter.CONTENT },
            Style.Empty.WithColor(0x808080));

    //TeamMessage TARGET+SENDER+CONTENT 对齐原版 teamMessage
    public static ChatTypeDecoration TeamMessage(string translationKey)
        => new(translationKey,
            new List<Parameter> { Parameter.TARGET, Parameter.SENDER, Parameter.CONTENT },
            Style.Empty);

    //Parameter 装饰参数枚举对齐原版 ChatTypeDecoration.Parameter
    //VarInt id 编解码 BY_ID 越界回退 SENDER
    public enum Parameter
    {
        SENDER = 0,
        TARGET = 1,
        CONTENT = 2,
    }
}

//ParameterExtensions Parameter 扩展方法必须放顶级静态类
public static class ParameterExtensions
{
    //ById 按 id 查 Parameter 越界回退 SENDER 对齐原版 ByIdMap.OutOfBoundsStrategy.ZERO
    public static ChatTypeDecoration.Parameter ById(int id)
        => id >= 0 && id <= 2 ? (ChatTypeDecoration.Parameter)id : ChatTypeDecoration.Parameter.SENDER;

    //GetName 返回小写名字对齐原版 getSerializedName
    public static string GetName(this ChatTypeDecoration.Parameter parameter) => parameter switch
    {
        ChatTypeDecoration.Parameter.SENDER => "sender",
        ChatTypeDecoration.Parameter.TARGET => "target",
        ChatTypeDecoration.Parameter.CONTENT => "content",
        _ => throw new ArgumentOutOfRangeException(nameof(parameter))
    };
}

//ChatTypeDecorationCodec 聊天类型装饰编解码 translationKey(字符串) + parameters(VarInt 长度前缀列表) + style
//Style 简化编解码用 packed byte 标志位 + 必要字段覆盖 color/bold/italic/underlined/strikethrough/obfuscated
internal sealed class ChatTypeDecorationCodec : StreamCodec<RegistryFriendlyByteBuf, ChatTypeDecoration>
{
    public ChatTypeDecoration Decode(RegistryFriendlyByteBuf buf)
    {
        var key = buf.ReadString();
        int count = buf.ReadVarInt();
        var parameters = new List<ChatTypeDecoration.Parameter>(count);
        for (int i = 0; i < count; i++)
            parameters.Add(ParameterExtensions.ById(buf.ReadVarInt()));
        var style = ReadStyle(buf);
        return new(key, parameters, style);
    }

    public void Encode(RegistryFriendlyByteBuf buf, ChatTypeDecoration value)
    {
        buf.WriteString(value.TranslationKey);
        buf.WriteVarInt(value.Parameters.Count);
        foreach (var p in value.Parameters)
            buf.WriteVarInt((int)p);
        WriteStyle(buf, value.Style);
    }

    //ReadStyle 简化 Style 解码 packed byte 标志位 + 对应字段
    internal static Style ReadStyle(RegistryFriendlyByteBuf buf)
    {
        byte flags = buf.ReadByte();
        var style = Style.Empty;
        if ((flags & 0x01) != 0)
        {
            var color = TextColor.ParseColor(buf.ReadString());
            if (color is not null) style = style.WithColor(color);
        }
        if ((flags & 0x02) != 0) style = style.WithBold(true);
        if ((flags & 0x04) != 0) style = style.WithItalic(true);
        if ((flags & 0x08) != 0) style = style.WithUnderlined(true);
        if ((flags & 0x10) != 0) style = style.WithStrikethrough(true);
        if ((flags & 0x20) != 0) style = style.WithObfuscated(true);
        return style;
    }

    //WriteStyle 简化 Style 编码 packed byte 标志位 + 对应字段
    internal static void WriteStyle(RegistryFriendlyByteBuf buf, Style style)
    {
        byte flags = 0;
        if (style.Color is not null) flags |= 0x01;
        if (style.IsBold) flags |= 0x02;
        if (style.IsItalic) flags |= 0x04;
        if (style.IsUnderlined) flags |= 0x08;
        if (style.IsStrikethrough) flags |= 0x10;
        if (style.IsObfuscated) flags |= 0x20;
        buf.WriteByte(flags);
        if (style.Color is not null) buf.WriteString(style.Color.Serialize());
    }
}
