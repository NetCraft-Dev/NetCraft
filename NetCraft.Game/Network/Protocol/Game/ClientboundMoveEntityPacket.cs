namespace NetCraft.Game.Network.Protocol.Game;

//ClientboundMoveEntityPacket move entity packet, maps to vanilla ClientboundMoveEntityPacket
//Abstract base with three nested subclasses Pos/PosRot/Rot sharing entityId/xa/ya/za/yRot/xRot/onGround/hasRot/hasPos fields
public abstract class ClientboundMoveEntityPacket : Packet<ClientGamePacketListener>
{
    public int EntityId { get; }
    public short Xa { get; }
    public short Ya { get; }
    public short Za { get; }
    public byte YRot { get; }
    public byte XRot { get; }
    public bool OnGround { get; }
    public bool HasRot { get; }
    public bool HasPos { get; }

    protected ClientboundMoveEntityPacket(int entityId, short xa, short ya, short za, byte yRot, byte xRot, bool onGround, bool hasRot, bool hasPos)
    {
        EntityId = entityId;
        Xa = xa;
        Ya = ya;
        Za = za;
        YRot = yRot;
        XRot = xRot;
        OnGround = onGround;
        HasRot = hasRot;
        HasPos = hasPos;
    }

    public abstract PacketType<ClientGamePacketListener> Type { get; }

    public void Handle(ClientGamePacketListener handler) => handler.HandleMoveEntity(this);

    //Pos position-only subclass, maps to vanilla ClientboundMoveEntityPacket Pos
    public sealed class Pos : ClientboundMoveEntityPacket
    {
        public static StreamCodec<FriendlyByteBuf, Pos> StreamCodec { get; } = new PosCodec();

        public Pos(int entityId, short xa, short ya, short za, bool onGround)
            : base(entityId, xa, ya, za, 0, 0, onGround, false, true) { }

        public override PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundMoveEntityPos;

        private sealed class PosCodec : StreamCodec<FriendlyByteBuf, Pos>
        {
            public Pos Decode(FriendlyByteBuf buf)
                => new(buf.ReadVarInt(), buf.ReadShort(), buf.ReadShort(), buf.ReadShort(), buf.ReadBoolean());

            public void Encode(FriendlyByteBuf buf, Pos value)
            {
                buf.WriteVarInt(value.EntityId);
                buf.WriteShort(value.Xa);
                buf.WriteShort(value.Ya);
                buf.WriteShort(value.Za);
                buf.WriteBoolean(value.OnGround);
            }
        }
    }

    //PosRot position-plus-rotation subclass, maps to vanilla ClientboundMoveEntityPacket PosRot
    public sealed class PosRot : ClientboundMoveEntityPacket
    {
        public static StreamCodec<FriendlyByteBuf, PosRot> StreamCodec { get; } = new PosRotCodec();

        public PosRot(int entityId, short xa, short ya, short za, byte yRot, byte xRot, bool onGround)
            : base(entityId, xa, ya, za, yRot, xRot, onGround, true, true) { }

        public override PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundMoveEntityPosRot;

        private sealed class PosRotCodec : StreamCodec<FriendlyByteBuf, PosRot>
        {
            public PosRot Decode(FriendlyByteBuf buf)
                => new(buf.ReadVarInt(), buf.ReadShort(), buf.ReadShort(), buf.ReadShort(), buf.ReadByte(), buf.ReadByte(), buf.ReadBoolean());

            public void Encode(FriendlyByteBuf buf, PosRot value)
            {
                buf.WriteVarInt(value.EntityId);
                buf.WriteShort(value.Xa);
                buf.WriteShort(value.Ya);
                buf.WriteShort(value.Za);
                buf.WriteByte(value.YRot);
                buf.WriteByte(value.XRot);
                buf.WriteBoolean(value.OnGround);
            }
        }
    }

    //Rot rotation-only subclass, maps to vanilla ClientboundMoveEntityPacket Rot
    public sealed class Rot : ClientboundMoveEntityPacket
    {
        public static StreamCodec<FriendlyByteBuf, Rot> StreamCodec { get; } = new RotCodec();

        public Rot(int entityId, byte yRot, byte xRot, bool onGround)
            : base(entityId, 0, 0, 0, yRot, xRot, onGround, true, false) { }

        public override PacketType<ClientGamePacketListener> Type => GamePacketTypes.ClientboundMoveEntityRot;

        private sealed class RotCodec : StreamCodec<FriendlyByteBuf, Rot>
        {
            public Rot Decode(FriendlyByteBuf buf)
                => new(buf.ReadVarInt(), buf.ReadByte(), buf.ReadByte(), buf.ReadBoolean());

            public void Encode(FriendlyByteBuf buf, Rot value)
            {
                buf.WriteVarInt(value.EntityId);
                buf.WriteByte(value.YRot);
                buf.WriteByte(value.XRot);
                buf.WriteBoolean(value.OnGround);
            }
        }
    }
}
