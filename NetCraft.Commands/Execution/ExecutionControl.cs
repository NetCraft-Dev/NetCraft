namespace NetCraft.Commands.Execution;

//ExecutionControl 自定义执行器对队列的受控入口对应原版 net.minecraft.commands.execution.ExecutionControl
//包装上下文与当前帧 入队的动作都记在当前帧深度下
public interface ExecutionControl<T>
{
    //QueueNext 追加一条动作
    void QueueNext(EntryAction<T> action);

    //Tracer 读写追踪器
    void Tracer(TraceCallbacks? tracer);

    TraceCallbacks? Tracer();

    //CurrentFrame 当前帧
    Frame CurrentFrame();

    //Create 绑定上下文与帧
    public static ExecutionControl<T> Create(ExecutionContext<T> context, Frame frame)
    {
        return new BoundControl(context, frame);
    }

    private sealed class BoundControl(ExecutionContext<T> context, Frame frame) : ExecutionControl<T>
    {
        public void QueueNext(EntryAction<T> action)
            => context.QueueNext(new CommandQueueEntry<T>(frame, action));

        public void Tracer(TraceCallbacks? tracer) => context.Tracer(tracer);

        public TraceCallbacks? Tracer() => context.Tracer();

        public Frame CurrentFrame() => frame;
    }
}
