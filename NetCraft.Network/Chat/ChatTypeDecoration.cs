namespace NetCraft.Network.Chat;

//ChatTypeDecoration chat type decoration, maps to vanilla net.minecraft.network.chat.ChatTypeDecoration
//A translationKey + parameters + style triple, used by Bound.decorate to build the translatable component
//Parameter enum aligns with vanilla SENDER/TARGET/CONTENT, coded by VarInt id
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

    //WithSender only SENDER+CONTENT parameters, aligns with vanilla withSender
    public static ChatTypeDecoration WithSender(string translationKey)
        => new(translationKey, new List<Parameter> { Parameter.SENDER, Parameter.CONTENT }, Style.Empty);

    //IncomingDirectMessage gray italic TARGET+CONTENT, aligns with vanilla incomingDirectMessage
    public static ChatTypeDecoration IncomingDirectMessage(string translationKey)
        => new(translationKey,
            new List<Parameter> { Parameter.SENDER, Parameter.CONTENT },
            Style.Empty.WithColor(0x808080));

    //OutgoingDirectMessage gray italic TARGET+CONTENT, aligns with vanilla outgoingDirectMessage
    public static ChatTypeDecoration OutgoingDirectMessage(string translationKey)
        => new(translationKey,
            new List<Parameter> { Parameter.TARGET, Parameter.CONTENT },
            Style.Empty.WithColor(0x808080));

    //TeamMessage TARGET+SENDER+CONTENT, aligns with vanilla teamMessage
    public static ChatTypeDecoration TeamMessage(string translationKey)
        => new(translationKey,
            new List<Parameter> { Parameter.TARGET, Parameter.SENDER, Parameter.CONTENT },
            Style.Empty);

    //Parameter decoration parameter enum, aligns with vanilla ChatTypeDecoration.Parameter
    //Coded by VarInt id; out-of-range falls back to SENDER via BY_ID
    public enum Parameter
    {
        SENDER = 0,
        TARGET = 1,
        CONTENT = 2,
    }
}

//ParameterExtensions extension methods for Parameter, which must live in a top-level static class
public static class ParameterExtensions
{
    //ById looks up Parameter by id and falls back to SENDER when out of range, aligns with vanilla ByIdMap.OutOfBoundsStrategy.ZERO
    public static ChatTypeDecoration.Parameter ById(int id)
        => id >= 0 && id <= 2 ? (ChatTypeDecoration.Parameter)id : ChatTypeDecoration.Parameter.SENDER;

    //GetName returns the lowercase name, aligns with vanilla getSerializedName
    public static string GetName(this ChatTypeDecoration.Parameter parameter) => parameter switch
    {
        ChatTypeDecoration.Parameter.SENDER => "sender",
        ChatTypeDecoration.Parameter.TARGET => "target",
        ChatTypeDecoration.Parameter.CONTENT => "content",
        _ => throw new ArgumentOutOfRangeException(nameof(parameter))
    };
}

//ChatTypeDecorationCodec codes the chat type decoration: translationKey(string) + parameters(VarInt length-prefixed list) + style
//The simplified Style codec uses a packed byte of flags + the necessary fields covering color/bold/italic/underlined/strikethrough/obfuscated
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

    //ReadStyle simplified Style decode: packed byte flags + corresponding fields
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

    //WriteStyle simplified Style encode: packed byte flags + corresponding fields
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
