using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Tree;
using NetCraft.Game.Commands.Synchronization;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundCommandsPacket command tree packet, maps to vanilla ClientboundCommandsPacket
//First enumerate the whole tree into a node table (root is fixed at 0), then write the nodes one by one; the client uses this to parse slash commands typed by the player
public sealed record ClientboundCommandsPacket(CommandNode<CommandSourceStack> Root) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundCommandsPacket> StreamCodec { get; } = new CommandsCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundCommands;

    public void Handle(ClientGamePacketListener handler) => handler.HandleCommands(this);

    //In flags the low two bits are the node type and the high bits are capability markers, aligns with vanilla
    private const byte MaskType = 3;
    private const byte TypeRoot = 0;
    private const byte TypeLiteral = 1;
    private const byte TypeArgument = 2;
    private const byte FlagExecutable = 4;
    private const byte FlagRedirect = 8;
    private const byte FlagCustomSuggestions = 16;

    private static readonly Predicate<CommandSourceStack> AlwaysTrue = _ => true;

    private sealed class CommandsCodec : StreamCodec<FriendlyByteBuf, ClientboundCommandsPacket>
    {
        public ClientboundCommandsPacket Decode(FriendlyByteBuf buf)
        {
            var count = buf.ReadVarInt();
            var entries = new Entry[count];
            var nodes = new CommandNode<CommandSourceStack>?[count];

            //children and redirect reference table-wide ids and are resolved lazily by recursion when nodes are built, aligns with vanilla NodeResolver
            for (var i = 0; i < count; i++) entries[i] = ReadEntry(buf);
            var rootIndex = buf.ReadVarInt();
            return new ClientboundCommandsPacket(Resolve(rootIndex, entries, nodes));
        }

        //Resolve lazily builds nodes and attaches children; a redirect target may be a forward reference, so it is built first when needed
        private static CommandNode<CommandSourceStack> Resolve(int index, Entry[] entries, CommandNode<CommandSourceStack>?[] nodes)
        {
            if (nodes[index] is { } cached) return cached;
            var node = CreateNode(entries[index], i => Resolve(i, entries, nodes));
            nodes[index] = node;
            foreach (var childId in entries[index].Children)
            {
                var child = Resolve(childId, entries, nodes);
                if (child is not RootCommandNode<CommandSourceStack>)
                    node.AddChild(child);
            }
            return node;
        }

        public void Encode(FriendlyByteBuf buf, ClientboundCommandsPacket value)
        {
            var order = new List<CommandNode<CommandSourceStack>>();
            var ids = new Dictionary<CommandNode<CommandSourceStack>, int>();
            Enumerate(value.Root, ids, order);

            buf.WriteVarInt(order.Count);
            foreach (var node in order) WriteNode(buf, node, ids);
            buf.WriteVarInt(ids[value.Root]);
        }
    }

    //Entry raw data of a single node, the product of the first decode pass
    private sealed class Entry
    {
        public byte Flags;
        public int[] Children = Array.Empty<int>();
        public int RedirectId = -1;
        public string Name = string.Empty;
        public object? ArgumentType;
    }

    //Enumerate collects nodes depth-first and assigns ids; redirect comes before children, matching vanilla
    private static void Enumerate(CommandNode<CommandSourceStack> node, Dictionary<CommandNode<CommandSourceStack>, int> ids, List<CommandNode<CommandSourceStack>> order)
    {
        if (ids.ContainsKey(node)) return;
        ids[node] = ids.Count;
        order.Add(node);
        if (node.GetRedirect() is { } redirect) Enumerate(redirect, ids, order);
        foreach (var child in node.GetChildren()) Enumerate(child, ids, order);
    }

    private static void WriteNode(FriendlyByteBuf buf, CommandNode<CommandSourceStack> node, Dictionary<CommandNode<CommandSourceStack>, int> ids)
    {
        byte flags;
        if (node is RootCommandNode<CommandSourceStack>) flags = TypeRoot;
        else if (node is LiteralCommandNode<CommandSourceStack>) flags = TypeLiteral;
        else if (node is ArgumentCommandNode<CommandSourceStack>) flags = TypeArgument;
        else throw new InvalidOperationException($"Unknown command node type: {node.GetType().Name}");

        var argument = node as ArgumentCommandNode<CommandSourceStack>;
        if (node.GetCommand() != null) flags |= FlagExecutable;
        if (node.GetRedirect() != null) flags |= FlagRedirect;
        if (argument?.CustomSuggestions != null) flags |= FlagCustomSuggestions;
        buf.WriteByte(flags);

        var children = node.GetChildren();
        buf.WriteVarInt(children.Count);
        foreach (var child in children) buf.WriteVarInt(ids[child]);
        if (node.GetRedirect() is { } redirect) buf.WriteVarInt(ids[redirect]);

        if (argument != null)
        {
            var argumentType = argument.GetArgumentTypeObject();
            var info = ArgumentTypeInfos.Unpack(argumentType)
                ?? throw new InvalidOperationException($"Unregistered argument type: {argumentType.GetType().Name}");
            buf.WriteString(argument.Name);
            buf.WriteVarInt(BuiltInRegistries.COMMAND_ARGUMENT_TYPE.GetIdOrThrow(info));
            info.SerializeToNetwork(info.Unpack(argumentType), buf);
            //Custom suggestions all fall back to ask_server, asking the server while the client types, aligns with vanilla unnamed provider behavior
            if ((flags & FlagCustomSuggestions) != 0)
                buf.WriteIdentifier(Identifier.WithDefaultNamespace("ask_server"));
        }
        else if (node is LiteralCommandNode<CommandSourceStack> literal)
        {
            buf.WriteString(literal.GetLiteral());
        }
    }

    private static Entry ReadEntry(FriendlyByteBuf buf)
    {
        var entry = new Entry { Flags = buf.ReadByte() };

        var childCount = buf.ReadVarInt();
        var children = new int[childCount];
        for (var i = 0; i < childCount; i++) children[i] = buf.ReadVarInt();
        entry.Children = children;
        if ((entry.Flags & FlagRedirect) != 0) entry.RedirectId = buf.ReadVarInt();

        var type = (byte)(entry.Flags & MaskType);
        if (type == TypeArgument)
        {
            entry.Name = buf.ReadString();
            var infoId = buf.ReadVarInt();
            var info = BuiltInRegistries.COMMAND_ARGUMENT_TYPE.ById(infoId) as ArgumentTypeInfo
                ?? throw new InvalidOperationException($"Unknown command argument type id: {infoId}");
            entry.ArgumentType = info.DeserializeFromNetwork(buf).Instantiate();
            if ((entry.Flags & FlagCustomSuggestions) != 0) buf.ReadIdentifier();
        }
        else if (type == TypeLiteral)
        {
            entry.Name = buf.ReadString();
        }

        return entry;
    }

    //CreateNode restores a node; redirect is resolved lazily through the resolve callback, aligns with vanilla resultBuilder.redirect
    private static CommandNode<CommandSourceStack> CreateNode(Entry entry, Func<int, CommandNode<CommandSourceStack>> resolve)
    {
        var redirect = entry.RedirectId >= 0 ? resolve(entry.RedirectId) : null;
        return (byte)(entry.Flags & MaskType) switch
        {
            TypeRoot => new RootCommandNode<CommandSourceStack>(),
            TypeLiteral => new LiteralCommandNode<CommandSourceStack>(entry.Name, null, AlwaysTrue, redirect, null, false),
            TypeArgument => CreateArgumentNode(entry, redirect),
            var other => throw new InvalidOperationException($"Unknown command node type bits: {other}"),
        };
    }

    private static CommandNode<CommandSourceStack> CreateArgumentNode(Entry entry, CommandNode<CommandSourceStack>? redirect)
    {
        var argumentType = entry.ArgumentType
            ?? throw new InvalidOperationException("Argument node is missing its argument type");
        var valueType = FindArgumentValueType(argumentType.GetType())
            ?? throw new InvalidOperationException($"Argument type does not implement ArgumentType<T>: {argumentType.GetType().Name}");

        //ArgumentCommandNode<S,T>'s T is only determined at runtime by the argument type instance, so it can only be constructed by reflection
        var nodeType = typeof(ArgumentCommandNode<,>).MakeGenericType(typeof(CommandSourceStack), valueType);
        var constructor = nodeType.GetConstructors()[0];
        return (CommandNode<CommandSourceStack>)constructor.Invoke(
            new object?[] { entry.Name, argumentType, null, AlwaysTrue, redirect, null, false, null })!;
    }

    private static Type? FindArgumentValueType(Type type)
    {
        foreach (var contract in type.GetInterfaces())
        {
            if (contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(ArgumentType<>))
            {
                return contract.GetGenericArguments()[0];
            }
        }
        return null;
    }
}
