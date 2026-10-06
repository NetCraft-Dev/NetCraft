namespace NetCraft.Storage;

//Ticket, one chunk ticket, maps to vanilla net.minecraft.server.level.Ticket
//A ticket = type (carrying behavior) + level (lower is stronger) + remaining ticks (meaningful only for timed types)
public sealed class Ticket
{
    public TicketType Type { get; }
    public int Level { get; }

    //TicksLeft, remaining ticks; timed types decrement each tick and expire when it goes negative
    public long TicksLeft { get; private set; }

    //Constructor initializing remaining ticks from the type's own timeout, maps to vanilla Ticket(type, level)
    public Ticket(TicketType type, int level) : this(type, level, type.Timeout) { }

    //Constructor taking remaining ticks explicitly, used when restoring from disk
    public Ticket(TicketType type, int level, long ticksLeft)
    {
        Type = type;
        Level = level;
        TicksLeft = ticksLeft;
    }

    //ResetTicksLeft renews the ticket, maps to vanilla resetTicksLeft
    public void ResetTicksLeft() => TicksLeft = Type.Timeout;

    //DecreaseTicksLeft decrements remaining ticks, only for timed types, maps to vanilla decreaseTicksLeft
    public void DecreaseTicksLeft()
    {
        if (Type.HasTimeout) TicksLeft--;
    }

    //IsTimedOut, whether it has expired, maps to vanilla isTimedOut
    public bool IsTimedOut => Type.HasTimeout && TicksLeft < 0;

    //IsSameTypeAndLevel treats same type and level as the same ticket, maps to vanilla isTicketSameTypeAndLevel
    public bool IsSameTypeAndLevel(Ticket other)
        => ReferenceEquals(Type, other.Type) && Level == other.Level;

    public override string ToString() => $"{Type}[{Level}] remaining {TicksLeft}";
}
