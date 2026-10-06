namespace NetCraft.Commands;

//CommandResultCallback 命令结果回调对应原版 net.minecraft.commands.CommandResultCallback
//执行链用 /return 把值逐层交回 Frame 回调链条
public interface CommandResultCallback
{
    //OnResult 结果回调 success 为假时 result 恒为 0
    void OnResult(bool success, int result);

    //OnSuccess 只回成功值
    void OnSuccess(int result) => OnResult(true, result);

    //OnFailure 只回失败
    void OnFailure() => OnResult(false, 0);

    //Chain 串两个回调空回调直接短路 对应原版 chain
    public static CommandResultCallback Chain(CommandResultCallback first, CommandResultCallback second)
    {
        if (ReferenceEquals(first, Empty)) return second;
        if (ReferenceEquals(second, Empty)) return first;
        return new ChainedCallback(first, second);
    }

    //Empty 恒空回调
    public static readonly CommandResultCallback Empty = new EmptyCallback();

    private sealed class EmptyCallback : CommandResultCallback
    {
        public void OnResult(bool success, int result)
        {
        }

        public override string ToString() => "<empty>";
    }

    private sealed class ChainedCallback : CommandResultCallback
    {
        private readonly CommandResultCallback _first;
        private readonly CommandResultCallback _second;

        public ChainedCallback(CommandResultCallback first, CommandResultCallback second)
        {
            _first = first;
            _second = second;
        }

        public void OnResult(bool success, int result)
        {
            _first.OnResult(success, result);
            _second.OnResult(success, result);
        }
    }
}
