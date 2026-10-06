namespace NetCraft.Util;

//Exception collector, maps to vanilla ExceptionCollector
//Collects multiple exceptions and throws them together, for multi-resource cleanup such as close
public sealed class ExceptionCollector<T> where T : Exception
{
    private List<T>? _exceptions;

    public bool HasExceptions => _exceptions != null && _exceptions.Count > 0;

    public void Add(T exception)
    {
        _exceptions ??= new List<T>();
        _exceptions.Add(exception);
    }

    //A single exception is thrown directly, multiple are aggregated into AggregateException
    public void ThrowIfPresent()
    {
        if (_exceptions == null || _exceptions.Count == 0) return;
        if (_exceptions.Count == 1) throw _exceptions[0];
        throw new AggregateException(_exceptions);
    }
}
