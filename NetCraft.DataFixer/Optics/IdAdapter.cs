namespace NetCraft.DataFixer.Optics;

using System;

//IdAdapter恒等适配器对应原版com.mojang.datafixers.optics.IdAdapter
//from/to直接返回原值S/T相同
internal sealed class IdAdapter<S, T> : Adapter<S, T, S, T>
{
    //单例缓存类型参数擦除后共享
    internal static readonly IdAdapter<object, object> Instance = new();

    private IdAdapter() { }

    public S From(S s) => s;
    public T To(T b) => b;

    public override string ToString() => "id";
}
