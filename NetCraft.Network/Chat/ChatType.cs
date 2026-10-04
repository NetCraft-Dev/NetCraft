using NetCraft.Registry;

namespace NetCraft.Network.Chat;

//ChatType 聊天类型对应原版 net.minecraft.network.chat.ChatType
//chat + narration 两个 ChatTypeDecoration 字段
//CHAT_TYPE 注册表用 object 弱类型避免跨层循环依赖 StreamCodec 手写通过 Lookup 查注册表 cast
//Bound 嵌套类绑定 Holder + name + targetName 用于业务层 decorate
public sealed class ChatType
{
    public ChatTypeDecoration Chat { get; }
    public ChatTypeDecoration Narration { get; }

    //DIRECT_STREAM_CODEC 直接编解码两个 ChatTypeDecoration 对应原版 DIRECT_STREAM_CODEC
    public static StreamCodec<RegistryFriendlyByteBuf, ChatType> DirectStreamCodec { get; }
        = new ChatTypeDirectCodec();

    //STREAM_CODEC 通过 CHAT_TYPE 注册表 id 编码 Holder 对应原版 STREAM_CODEC
    //DirectHolderStreamCodec 支持 Reference id+1 和 Direct id==0 两种形式
    //CHAT_TYPE 为 Registry<object> decode 时 cast object 为 ChatType
    public static StreamCodec<RegistryFriendlyByteBuf, Holder<ChatType>> StreamCodec { get; }
        = new ChatTypeHolderCodec();

    //DEFAULT_CHAT_DECORATION 默认装饰对齐原版 DEFAULT_CHAT_DECORATION
    public static readonly ChatTypeDecoration DefaultChatDecoration
        = ChatTypeDecoration.WithSender("chat.type.text");

    //CHAT/SAY_COMMAND 等 7 个 ResourceKey 对齐原版预定义 key
    //CHAT_TYPE 为 Registry<object> ResourceKey 也用 object 弱类型
    public static readonly ResourceKey<object> CHAT = Create("chat");
    public static readonly ResourceKey<object> SAY_COMMAND = Create("say_command");
    public static readonly ResourceKey<object> MSG_COMMAND_INCOMING = Create("msg_command_incoming");
    public static readonly ResourceKey<object> MSG_COMMAND_OUTGOING = Create("msg_command_outgoing");
    public static readonly ResourceKey<object> TEAM_MSG_COMMAND_INCOMING = Create("team_msg_command_incoming");
    public static readonly ResourceKey<object> TEAM_MSG_COMMAND_OUTGOING = Create("team_msg_command_outgoing");
    public static readonly ResourceKey<object> EMOTE_COMMAND = Create("emote_command");

    public ChatType(ChatTypeDecoration chat, ChatTypeDecoration narration)
    {
        Chat = chat;
        Narration = narration;
    }

    //Create 构造 ChatType ResourceKey 对齐原版 create
    private static ResourceKey<object> Create(string name)
        => ResourceKey<object>.Create(Registries.CHAT_TYPE, Identifier.WithDefaultNamespace(name));

    //Bound 绑定 Holder+name+targetName 用于业务层 decorate 对应原版 ChatType.Bound
    //STREAM_CODEC 编码 chatType(Holder) + name(Component) + targetName(Optional<Component>)
    public sealed class Bound
    {
        public Holder<ChatType> ChatType { get; }
        public Component Name { get; }
        public Component? TargetName { get; }

        public static StreamCodec<RegistryFriendlyByteBuf, Bound> StreamCodec { get; }
            = new ChatTypeBoundCodec();

        public Bound(Holder<ChatType> chatType, Component name, Component? targetName = null)
        {
            ChatType = chatType;
            Name = name;
            TargetName = targetName;
        }

        //WithTargetName 设置目标名返回新 Bound 对齐原版 withTargetName
        public Bound WithTargetName(Component targetName)
            => new(ChatType, Name, targetName);
    }
}

//ChatTypeDirectCodec 直接编解码 ChatType 两个 ChatTypeDecoration 字段
internal sealed class ChatTypeDirectCodec : StreamCodec<RegistryFriendlyByteBuf, ChatType>
{
    public ChatType Decode(RegistryFriendlyByteBuf buf)
    {
        var chat = ChatTypeDecoration.StreamCodec.Decode(buf);
        var narration = ChatTypeDecoration.StreamCodec.Decode(buf);
        return new(chat, narration);
    }

    public void Encode(RegistryFriendlyByteBuf buf, ChatType value)
    {
        ChatTypeDecoration.StreamCodec.Encode(buf, value.Chat);
        ChatTypeDecoration.StreamCodec.Encode(buf, value.Narration);
    }
}

//ChatTypeHolderCodec 通过 CHAT_TYPE 注册表 id 编解码 Holder<ChatType>
//CHAT_TYPE 为 Registry<object> decode 时 cast object 为 ChatType encode 时 cast ChatType 为 object
internal sealed class ChatTypeHolderCodec : StreamCodec<RegistryFriendlyByteBuf, Holder<ChatType>>
{
    private const int DirectHolderId = 0;

    public Holder<ChatType> Decode(RegistryFriendlyByteBuf buf)
    {
        int id = buf.ReadVarInt();
        if (id == DirectHolderId)
            return Holder<ChatType>.Direct(ChatType.DirectStreamCodec.Decode(buf));
        var registry = buf.Lookup(Registries.CHAT_TYPE);
        var holder = registry.Get(id - 1);
        if (holder is null)
            throw new InvalidOperationException($"未知 holder id {id} in CHAT_TYPE");
        var value = holder.Value as ChatType
            ?? throw new InvalidOperationException($"CHAT_TYPE 注册表值不是 ChatType: {holder.Value}");
        return Holder<ChatType>.Direct(value);
    }

    public void Encode(RegistryFriendlyByteBuf buf, Holder<ChatType> value)
    {
        if (value.HolderKind == Holder<ChatType>.Kind.Reference)
        {
            var registry = buf.Lookup(Registries.CHAT_TYPE);
            int id = registry.GetId((object)value.Value);
            if (id == IdMap<object>.Default)
                throw new InvalidOperationException($"holder 值未注册: {value.Value}");
            buf.WriteVarInt(id + 1);
        }
        else
        {
            buf.WriteVarInt(DirectHolderId);
            ChatType.DirectStreamCodec.Encode(buf, value.Value);
        }
    }
}

//ChatTypeBoundCodec ChatType.Bound 编解码 chatType(Holder) + name(Component) + targetName(nullable Component)
internal sealed class ChatTypeBoundCodec : StreamCodec<RegistryFriendlyByteBuf, ChatType.Bound>
{
    public ChatType.Bound Decode(RegistryFriendlyByteBuf buf)
    {
        var chatType = ChatType.StreamCodec.Decode(buf);
        var name = ComponentSerialization.StreamCodec.Decode(buf);
        Component? targetName = buf.ReadBoolean() ? ComponentSerialization.StreamCodec.Decode(buf) : null;
        return new(chatType, name, targetName);
    }

    public void Encode(RegistryFriendlyByteBuf buf, ChatType.Bound value)
    {
        ChatType.StreamCodec.Encode(buf, value.ChatType);
        ComponentSerialization.StreamCodec.Encode(buf, value.Name);
        bool hasTarget = value.TargetName is not null;
        buf.WriteBoolean(hasTarget);
        if (hasTarget) ComponentSerialization.StreamCodec.Encode(buf, value.TargetName!);
    }
}
