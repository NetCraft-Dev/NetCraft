using System.Collections.Generic;

namespace NetCraft.Network.Protocol.Configuration;

//ClientboundRegistryDataPacket 服务端发送注册表数据
//对应原版 net.minecraft.network.protocol.configuration.ClientboundRegistryDataPacket
//原版依赖 ResourceKey 和 RegistrySynchronization.PackedRegistryEntry 简化为
//  Identifier RegistryKey 标识哪个注册表
//  byte[] Entries 透传原版 PackedRegistryEntry 列表的序列化字节(含 count 前缀 无外层长度)
//  EntriesCodec 直接把 Entries 追加到流 对齐原版 ByteBufCodecs.list() 布局
//  未来 RegistrySynchronization 内核实现后改回强类型解析
public sealed record ClientboundRegistryDataPacket(Identifier RegistryKey, byte[] Entries) : Packet<ClientConfigurationPacketListener>
{
    public static StreamCodec<FriendlyByteBuf, ClientboundRegistryDataPacket> StreamCodec { get; } = new RegistryDataCodec();

    public PacketType<ClientConfigurationPacketListener> Type => ConfigurationPacketTypes.ClientboundRegistryData;

    public void Handle(ClientConfigurationPacketListener handler) => handler.HandleRegistryData(this);

    private sealed class RegistryDataCodec : StreamCodec<FriendlyByteBuf, ClientboundRegistryDataPacket>
    {
        //原版 entries 是 list(count varint + PackedRegistryEntry...) 无外层字节长度前缀
        //WriteBytes 原样追加 PackBiomes 已含 count 前缀的字节 避免多包一层 VarInt 长度导致客户端错位
        public ClientboundRegistryDataPacket Decode(FriendlyByteBuf buf)
            => new(buf.ReadIdentifier(), buf.ReadBytes(buf.ReadableBytes));

        public void Encode(FriendlyByteBuf buf, ClientboundRegistryDataPacket value)
        {
            buf.WriteIdentifier(value.RegistryKey);
            buf.WriteBytes(value.Entries);
        }
    }
}
