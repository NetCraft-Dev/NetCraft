namespace NetCraft.Commands.Execution;

//ChainModifiers 执行链标记对应原版 net.minecraft.commands.execution.ChainModifiers
//forked 来自 /execute 分叉修饰 return 来自 /return 只有这两个标志
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

    //IsForked 是否处于分叉模式
    public bool IsForked() => (Flags & FlagForked) != 0;

    //SetForked 打上分叉标记
    public ChainModifiers SetForked() => SetFlag(FlagForked);

    //IsReturn 是否处于返回模式
    public bool IsReturn() => (Flags & FlagIsReturn) != 0;

    //SetReturn 打上返回标记
    public ChainModifiers SetReturn() => SetFlag(FlagIsReturn);
}
