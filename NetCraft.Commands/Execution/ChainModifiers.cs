namespace NetCraft.Commands.Execution;

//ChainModifiers execution chain flags, maps to vanilla net.minecraft.commands.execution.ChainModifiers
//forked comes from /execute fork modifiers, return comes from /return; there are just these two flags
public sealed record ChainModifiers(byte Flags)
{
    public static readonly ChainModifiers Default = new(0);
    private const byte FlagForked = 1;
    private const byte FlagIsReturn = 2;

    private ChainModifiers SetFlag(byte flag)
    {
        var newFlags = (byte)(Flags | flag);
        return newFlags != Flags ? new ChainModifiers(newFlags) : this;
    }

    //IsForked whether in fork mode
    public bool IsForked() => (Flags & FlagForked) != 0;

    //SetForked sets the fork flag
    public ChainModifiers SetForked() => SetFlag(FlagForked);

    //IsReturn whether in return mode
    public bool IsReturn() => (Flags & FlagIsReturn) != 0;

    //SetReturn sets the return flag
    public ChainModifiers SetReturn() => SetFlag(FlagIsReturn);
}
