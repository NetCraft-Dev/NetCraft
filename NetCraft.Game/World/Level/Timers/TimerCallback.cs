using NetCraft.Codec;

namespace NetCraft.Game.World.Level.Timers;

//TimerCallback scheduled event callback, maps to vanilla net.minecraft.world.level.timers.TimerCallback
//On trigger receives the server context and queue; custom callbacks can keep scheduling new events
//Made an abstract record so subclasses (such as FunctionCallback) keep the value semantics of the vanilla record
public abstract record TimerCallback<T>
{
    //Handle fires when due
    public abstract void Handle(T context, TimerQueue<T> queue, long time);

    //GetCodec gets the dispatch codec registered in TimerCallbacks
    public abstract MapCodec<TimerCallback<T>> GetCodec();
}
