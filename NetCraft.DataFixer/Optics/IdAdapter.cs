namespace NetCraft.DataFixer.Optics;

using System;

//IdAdapter identity adapter maps to vanilla com.mojang.datafixers.optics.IdAdapter
//from/to return the original value directly; S/T are the same
internal sealed class IdAdapter<S, T> : Adapter<S, T, S, T>
{
    //singleton cached, shared after type parameter erasure
    internal static readonly IdAdapter<object, object> Instance = new();

    private IdAdapter() { }

    public S From(S s) => s;
    public T To(T b) => b;

    public override string ToString() => "id";
}
