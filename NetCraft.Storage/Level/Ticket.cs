namespace NetCraft.Storage;

//Ticket 一张区块票对应原版 net.minecraft.server.level.Ticket
//票 = 类型(带行为) + 等级(数值越小越强) + 剩余 tick(只对有超时的类型有意义)
public sealed class Ticket
{
    public TicketType Type { get; }
    public int Level { get; }

    //TicksLeft 剩余 tick 超时类型每 tick 递减 减到负数即过期
    public long TicksLeft { get; private set; }

    //构造按类型自带超时初始化剩余 tick 对应原版 Ticket(type, level)
    public Ticket(TicketType type, int level) : this(type, level, type.Timeout) { }

    //构造显式给剩余 tick 供读盘还原用
    public Ticket(TicketType type, int level, long ticksLeft)
    {
        Type = type;
        Level = level;
        TicksLeft = ticksLeft;
    }

    //ResetTicksLeft 续期对应原版 resetTicksLeft
    public void ResetTicksLeft() => TicksLeft = Type.Timeout;

    //DecreaseTicksLeft 递减剩余 tick 只对有超时的类型生效 对应原版 decreaseTicksLeft
    public void DecreaseTicksLeft()
    {
        if (Type.HasTimeout) TicksLeft--;
    }

    //IsTimedOut 是否已过期对应原版 isTimedOut
    public bool IsTimedOut => Type.HasTimeout && TicksLeft < 0;

    //IsSameTypeAndLevel 同类型同等级视为同一张票 对应原版 isTicketSameTypeAndLevel
    public bool IsSameTypeAndLevel(Ticket other)
        => ReferenceEquals(Type, other.Type) && Level == other.Level;

    public override string ToString() => $"{Type}[{Level}] 剩余 {TicksLeft}";
}
