using NetCraft.Codec;

namespace NetCraft.Game.World.Level.Timers;

//TimerCallback 计划事件的回调对应原版 net.minecraft.world.level.timers.TimerCallback
//触发时拿服务端上下文与队列 自定义回调可以继续排新事件
//做成 abstract record 让子类(如 FunctionCallback)的值语义与原版 record 一致
public abstract record TimerCallback<T>
{
    //Handle 到点触发
    public abstract void Handle(T context, TimerQueue<T> queue, long time);

    //GetCodec 取注册进 TimerCallbacks 的分派 codec
    public abstract MapCodec<TimerCallback<T>> GetCodec();
}
