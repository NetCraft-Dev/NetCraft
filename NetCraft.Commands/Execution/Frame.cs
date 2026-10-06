namespace NetCraft.Commands.Execution;

//FrameControl 帧废弃回调对应原版 Frame.FrameControl
public delegate void FrameControl();

//Frame 执行帧对应原版 net.minecraft.commands.execution.Frame
//深度决定出错时废弃到哪一层 返回值消费者接 /return 的值
public sealed record Frame(int Depth, CommandResultCallback ReturnValueConsumer, FrameControl FrameControl)
{
    //ReturnSuccess 回成功值
    public void ReturnSuccess(int value) => ReturnValueConsumer.OnSuccess(value);

    //ReturnFailure 回失败
    public void ReturnFailure() => ReturnValueConsumer.OnFailure();

    //Discard 废弃本帧及更深的队列项
    public void Discard() => FrameControl();
}
