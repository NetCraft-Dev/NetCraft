namespace NetCraft.Network;

//IPacket non-generic packet interface
//Under C# generic invariance Packet<parent listener> cannot convert to Packet<child listener>, so common packets dispatched while bridging must be routed by ID
public interface IPacket
{
    //PacketTypeId is the packet type's network ID
    //Not named TypeId because packets' own business fields are often called TypeId (block entity type/entity type)
    //A property of the same name would implicitly implement the interface member and override the default implementation, causing the network ID to be sent as a business field
    int PacketTypeId { get; }
}

//Packet protocol packet interface, maps to vanilla net.minecraft.network.protocol.Packet
//THandler is the protocol handler type, providing a Handle method that takes the handler
public interface Packet<THandler> : IPacket
{
    //PacketType is the packet type identity, used for registration and encoding
    PacketType<THandler> Type { get; }

    //IPacket.PacketTypeId is explicitly implemented to take the network ID from the generic Type
    int IPacket.PacketTypeId => Type.Id;

    //Handle calls the handler's corresponding method
    void Handle(THandler handler);

    //IsSkippable indicates whether the packet can be skipped, false by default, aligns with vanilla isSkippable
    //On a decode failure, a skippable packet is dropped and processing continues, otherwise an exception is thrown
    bool IsSkippable => false;

    //IsTerminal indicates whether the packet is terminal, false by default, aligns with vanilla isTerminal
    //A terminal packet closes the connection after handling, such as LoginDisconnectPacket
    bool IsTerminal => false;
}
