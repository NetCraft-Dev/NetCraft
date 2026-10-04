namespace NetCraft.Registry;

//带默认值的注册表接口，未注册的 key 返回默认值而非 null
//继承 WritableRegistry 对齐原版 DefaultedRegistry extends WritableRegistry
public interface DefaultedRegistry<T> : WritableRegistry<T> where T : class
{
    //默认注册名
    Identifier DefaultKey { get; }
}
