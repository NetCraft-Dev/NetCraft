using NetCraft.Registry;

namespace NetCraft.Network.Chat;

//ChatType chat type, maps to vanilla net.minecraft.network.chat.ChatType
//Two ChatTypeDecoration fields: chat + narration
//CHAT_TYPE registry uses weak-typed object to avoid cross-layer circular dependencies; the StreamCodec is hand-written, looking up the registry via Lookup and casting
//Bound nested class binds Holder + name + targetName, used by the business layer to decorate
public sealed class ChatType
{
    public ChatTypeDecoration Chat { get; }
    public ChatTypeDecoration Narration { get; }

    //DIRECT_STREAM_CODEC directly codes the two ChatTypeDecoration fields, maps to vanilla DIRECT_STREAM_CODEC
    public static StreamCodec<RegistryFriendlyByteBuf, ChatType> DirectStreamCodec { get; }
        = new ChatTypeDirectCodec();

    //STREAM_CODEC encodes Holder through the CHAT_TYPE registry id, maps to vanilla STREAM_CODEC
    //DirectHolderStreamCodec supports both Reference id+1 and Direct id==0 forms
    //CHAT_TYPE is a Registry<object>, so decode casts object to ChatType
    public static StreamCodec<RegistryFriendlyByteBuf, Holder<ChatType>> StreamCodec { get; }
        = new ChatTypeHolderCodec();

    //DEFAULT_CHAT_DECORATION default decoration, aligns with vanilla DEFAULT_CHAT_DECORATION
    public static readonly ChatTypeDecoration DefaultChatDecoration
        = ChatTypeDecoration.WithSender("chat.type.text");

    //7 ResourceKeys such as CHAT/SAY_COMMAND, aligning with the vanilla predefined keys
    //CHAT_TYPE is a Registry<object>, so its ResourceKey also uses weak-typed object
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

    //Create builds a ChatType ResourceKey, aligns with vanilla create
    private static ResourceKey<object> Create(string name)
        => ResourceKey<object>.Create(Registries.CHAT_TYPE, Identifier.WithDefaultNamespace(name));

    //Bound binds Holder+name+targetName for business-layer decorate, maps to vanilla ChatType.Bound
    //STREAM_CODEC encodes chatType(Holder) + name(Component) + targetName(Optional<Component>)
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

        //WithTargetName sets the target name and returns a new Bound, aligns with vanilla withTargetName
        public Bound WithTargetName(Component targetName)
            => new(ChatType, Name, targetName);
    }
}

//ChatTypeDirectCodec directly codes the two ChatTypeDecoration fields of ChatType
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

//ChatTypeHolderCodec codes Holder<ChatType> through the CHAT_TYPE registry id
//CHAT_TYPE is a Registry<object>, so decode casts object to ChatType and encode casts ChatType to object
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
            throw new InvalidOperationException($"unknown holder id {id} in CHAT_TYPE");
        var value = holder.Value as ChatType
            ?? throw new InvalidOperationException($"CHAT_TYPE registry value is not a ChatType: {holder.Value}");
        return Holder<ChatType>.Direct(value);
    }

    public void Encode(RegistryFriendlyByteBuf buf, Holder<ChatType> value)
    {
        if (value.HolderKind == Holder<ChatType>.Kind.Reference)
        {
            var registry = buf.Lookup(Registries.CHAT_TYPE);
            int id = registry.GetId((object)value.Value);
            if (id == IdMap<object>.Default)
                throw new InvalidOperationException($"holder value not registered: {value.Value}");
            buf.WriteVarInt(id + 1);
        }
        else
        {
            buf.WriteVarInt(DirectHolderId);
            ChatType.DirectStreamCodec.Encode(buf, value.Value);
        }
    }
}

//ChatTypeBoundCodec codes ChatType.Bound: chatType(Holder) + name(Component) + targetName(nullable Component)
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
