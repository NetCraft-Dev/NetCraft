using NetCraft.Commands;
using NetCraft.Commands.Arguments;
using NetCraft.Commands.Tree;
using NetCraft.Game.Commands.Synchronization;
using NetCraft.Primitives;
using NetCraft.Registry;

namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundCommandsPacket 命令树下发包对应原版 ClientboundCommandsPacket
//先把整棵树枚举成节点表(根节点固定 0)再逐个写节点 客户端据此解析玩家输入的斜杠命令
public sealed record ClientboundCommandsPacket(CommandNode<CommandSourceStack> Root) : Packet<ClientGamePacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundCommandsPacket> StreamCodec { get; } = new CommandsCodec();

    public PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundCommands;

    public void Handle(ClientGamePacketListener handler) => handler.HandleCommands(this);

    //flags 低两位是节点类型 高位是能力标记 对齐原版
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

            //children 与 redirect 引用整表 id 建节点时惰性递归解析 对齐原版 NodeResolver
            for (var i = 0; i < count; i++) entries[i] = ReadEntry(buf);
            var rootIndex = buf.ReadVarInt();
            return new ClientboundCommandsPacket(Resolve(rootIndex, entries, nodes));
        }

        //Resolve 惰性建节点并挂载 children redirect 目标可能前向引用 需要时先建
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

    //Entry 单个节点的原始数据 解码第一遍的产物
    private sealed class Entry
    {
        public byte Flags;
        public int[] Children = Array.Empty<int>();
        public int RedirectId = -1;
        public string Name = string.Empty;
        public object? ArgumentType;
    }

    //Enumerate 深度优先收集节点并分配 id redirect 先于 children 与原版一致
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
        else throw new InvalidOperationException($"未知命令节点类型: {node.GetType().Name}");

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
                ?? throw new InvalidOperationException($"参数类型未注册: {argumentType.GetType().Name}");
            buf.WriteString(argument.Name);
            buf.WriteVarInt(BuiltInRegistries.COMMAND_ARGUMENT_TYPE.GetIdOrThrow(info));
            info.SerializeToNetwork(info.Unpack(argumentType), buf);
            //自定义补全统一回落 ask_server 客户端打字时回问服务端 对齐原版未命名 provider 行为
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
                ?? throw new InvalidOperationException($"未知命令参数类型 id: {infoId}");
            entry.ArgumentType = info.DeserializeFromNetwork(buf).Instantiate();
            if ((entry.Flags & FlagCustomSuggestions) != 0) buf.ReadIdentifier();
        }
        else if (type == TypeLiteral)
        {
            entry.Name = buf.ReadString();
        }

        return entry;
    }

    //CreateNode 还原节点 redirect 经 resolve 回调惰性解析 对齐原版 resultBuilder.redirect
    private static CommandNode<CommandSourceStack> CreateNode(Entry entry, Func<int, CommandNode<CommandSourceStack>> resolve)
    {
        var redirect = entry.RedirectId >= 0 ? resolve(entry.RedirectId) : null;
        return (byte)(entry.Flags & MaskType) switch
        {
            TypeRoot => new RootCommandNode<CommandSourceStack>(),
            TypeLiteral => new LiteralCommandNode<CommandSourceStack>(entry.Name, null, AlwaysTrue, redirect, null, false),
            TypeArgument => CreateArgumentNode(entry, redirect),
            var other => throw new InvalidOperationException($"未知命令节点类型位: {other}"),
        };
    }

    private static CommandNode<CommandSourceStack> CreateArgumentNode(Entry entry, CommandNode<CommandSourceStack>? redirect)
    {
        var argumentType = entry.ArgumentType
            ?? throw new InvalidOperationException("参数节点缺少参数类型");
        var valueType = FindArgumentValueType(argumentType.GetType())
            ?? throw new InvalidOperationException($"参数类型未实现 ArgumentType<T>: {argumentType.GetType().Name}");

        //ArgumentCommandNode<S,T> 的 T 只在运行时由参数类型实例决定 只能反射构造
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
