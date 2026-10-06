using NetCraft.Registry;

namespace NetCraft.Network;

//IPacketType non-generic packet type interface
//PacketTypeRegistry indexes by (Protocol, Direction, Id) without depending on THandler, so this interface is used
public interface IPacketType
{
    //Id is the simplified network ID, assigned by registration order
    int Id { get; }
    //Protocol is the owning protocol
    ConnectionProtocol Protocol { get; }
    //Direction matches FlowDirection
    FlowDirection Direction { get; }
}

//PacketType packet type, maps to vanilla net.minecraft.network.protocol.PacketType
//Registered into PacketTypeRegistry, each protocol packet has a unique id
//Supports both the simplified int Id (used by Connection.cs) and the vanilla Identifier (used by ProtocolInfoBuilder)
public sealed class PacketType<THandler> : IPacketType
{
    //Id is the simplified network ID, assigned by registration order
    public int Id { get; }
    //Protocol is the owning protocol
    public ConnectionProtocol Protocol { get; }
    //Direction matches FlowDirection
    public FlowDirection Direction { get; }
    //Codec is the packet codec
    public StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>>? Codec { get; }
    //Identifier is the vanilla identifier, aligns with vanilla PacketType.id
    //Null by default, can be left unset when the vanilla system is not required
    public Identifier? Identifier { get; set; }

    public PacketType(int id, ConnectionProtocol protocol, FlowDirection direction, StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>>? codec)
    {
        Id = id;
        Protocol = protocol;
        Direction = direction;
        Codec = codec;
    }

    //WithIdentifier sets the vanilla Identifier and returns this for chaining
    public PacketType<THandler> WithIdentifier(Identifier identifier)
    {
        Identifier = identifier;
        return this;
    }

    public override string ToString() =>
        Identifier.HasValue ? $"{Protocol.Id()}/{Direction}/{Identifier.Value}" : $"PacketType[{Protocol}/{Direction} #{Id}]";
}

//FlowDirection packet direction, maps to vanilla PacketFlow
//Clientbound server to client
//Serverbound client to server
public enum FlowDirection
{
    Clientbound,
    Serverbound
}

//FlowDirectionExtensions extension methods for FlowDirection, aligns with vanilla PacketFlow.id/getOpposite
public static class FlowDirectionExtensions
{
    //Id returns the lowercase direction name string
    public static string Id(this FlowDirection direction) => direction switch
    {
        FlowDirection.Clientbound => "clientbound",
        FlowDirection.Serverbound => "serverbound",
        _ => direction.ToString().ToLowerInvariant(),
    };

    //GetOpposite returns the opposite direction
    public static FlowDirection GetOpposite(this FlowDirection direction) =>
        direction == FlowDirection.Clientbound ? FlowDirection.Serverbound : FlowDirection.Clientbound;

    //ToPacketFlow converts to Protocol.PacketFlow
    public static Protocol.PacketFlow ToPacketFlow(this FlowDirection direction) =>
        direction == FlowDirection.Clientbound ? Protocol.PacketFlow.Clientbound : Protocol.PacketFlow.Serverbound;
}
