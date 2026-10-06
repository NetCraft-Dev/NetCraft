namespace NetCraft.Commands.Execution;

//FrameControl frame discard callback, maps to vanilla Frame.FrameControl
public delegate void FrameControl();

//Frame execution frame, maps to vanilla net.minecraft.commands.execution.Frame
//The depth decides how far to discard on error; the return value consumer receives the value of /return
public sealed record Frame(int Depth, CommandResultCallback ReturnValueConsumer, FrameControl FrameControl)
{
    //ReturnSuccess reports the success value
    public void ReturnSuccess(int value) => ReturnValueConsumer.OnSuccess(value);

    //ReturnFailure reports failure
    public void ReturnFailure() => ReturnValueConsumer.OnFailure();

    //Discard discards queue entries at this frame and deeper
    public void Discard() => FrameControl();
}
