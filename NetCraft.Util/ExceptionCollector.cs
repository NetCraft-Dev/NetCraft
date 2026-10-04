namespace NetCraft.Util;

//异常收集器对应原版ExceptionCollector
//收集多个异常后统一抛出，用于close等多资源清理场景
public sealed class ExceptionCollector<T> where T : Exception
{
    private List<T>? _exceptions;

    public bool HasExceptions => _exceptions != null && _exceptions.Count > 0;

    public void Add(T exception)
    {
        _exceptions ??= new List<T>();
        _exceptions.Add(exception);
    }

    //单个异常直接抛，多个聚合为AggregateException
    public void ThrowIfPresent()
    {
        if (_exceptions == null || _exceptions.Count == 0) return;
        if (_exceptions.Count == 1) throw _exceptions[0];
        throw new AggregateException(_exceptions);
    }
}
