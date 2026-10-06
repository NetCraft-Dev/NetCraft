namespace NetCraft.Network.Protocol;

//ProtocolInfo protocol info, maps to vanilla net.minecraft.network.protocol.ProtocolInfo
//Protocol info after binding a context, containing the codec and bundling info
//THandler is the packet handler type
//Inherits INonGenericProtocol so Connection can store the current protocol without depending on THandler
public interface ProtocolInfo<THandler> : INonGenericProtocol
{
    //Id protocol enum (new hides INonGenericProtocol.Id, using the generic protocol's Id)
    new ConnectionProtocol Id { get; }

    //Flow packet direction
    PacketFlow Flow { get; }

    //Codec is the packet codec
    StreamCodec<RegistryFriendlyByteBuf, Packet<THandler>> Codec { get; }

    //BundlerInfo packet bundling info
    BundlerInfo<THandler> BundlerInfo { get; }

    //Explicitly implements INonGenericProtocol.FlowDirection, converted from PacketFlow
    FlowDirection INonGenericProtocol.FlowDirection
        => Flow == PacketFlow.Clientbound ? FlowDirection.Clientbound : FlowDirection.Serverbound;

    //Explicitly implements INonGenericProtocol.DecodePacket, delegating to Codec.Decode
    object? INonGenericProtocol.DecodePacket(RegistryFriendlyByteBuf buf) => Codec.Decode(buf);

    //Explicitly implements INonGenericProtocol.EncodePacket, delegating to Codec.Encode
    //common packets have the static type Packet<parent listener> and cannot be cast to Packet<THandler>, so the object bridge path takes priority
    void INonGenericProtocol.EncodePacket(RegistryFriendlyByteBuf buf, object packet)
    {
        if (Codec is IObjectEncodable encodable)
        {
            encodable.EncodeObject(buf, packet);
            return;
        }
        Codec.Encode(buf, (Packet<THandler>)packet);
    }

    //Explicitly implements INonGenericProtocol.PacketIdFor, taking the packet's Type.Id
    int INonGenericProtocol.PacketIdFor(object packet)
        => packet is IPacket p ? p.PacketTypeId : ((Packet<THandler>)packet).Type.Id;

    //Details protocol static details listing all packets by direction
    public interface Details
    {
        //Id protocol enum
        ConnectionProtocol Id { get; }

        //Flow direction
        PacketFlow Flow { get; }

        //ListPackets traverses all packet types and their network IDs
        void ListPackets(PacketVisitor output);

        //PacketVisitor packet visitor
        public interface PacketVisitor
        {
            void Accept(PacketType<THandler> type, int networkId);
        }
    }

    //DetailsProvider provides Details
    public interface DetailsProvider
    {
        Details Details { get; }
    }
}
