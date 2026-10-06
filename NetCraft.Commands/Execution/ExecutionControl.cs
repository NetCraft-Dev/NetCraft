namespace NetCraft.Commands.Execution;

//ExecutionControl controlled entry point to the queue for custom executors, maps to vanilla net.minecraft.commands.execution.ExecutionControl
//Wraps the context and the current frame; enqueued actions are recorded under the current frame depth
public interface ExecutionControl<T>
{
    //QueueNext appends an action
    void QueueNext(EntryAction<T> action);

    //Tracer read/write the tracer
    void Tracer(TraceCallbacks? tracer);

    TraceCallbacks? Tracer();

    //CurrentFrame current frame
    Frame CurrentFrame();

    //Create binds a context and a frame
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
