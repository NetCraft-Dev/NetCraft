using NetCraft.Codec;

namespace NetCraft.Util.Parsing.Packrat;

//解析状态对应原版net.minecraft.util.parsing.packrat.ParseState
//持有scope和errorCollector提供规则解析入口和游标mark/restore
//silent返回不收集错误的状态
public interface ParseState<S>
{
    Scope Scope { get; }

    ErrorCollector<S> ErrorCollector { get; }

    T Parse<T>(NamedRule<S, T> rule);

    S Input { get; }

    int Mark();

    void Restore(int mark);

    Control AcquireControl();

    void ReleaseControl();

    ParseState<S> Silent { get; }
}

public static class ParseStateExtensions
{
    //parseTopRule解析顶层规则返回Optional结果
    public static Optional<T> ParseTopRule<S, T>(this ParseState<S> state, NamedRule<S, T> rule)
    {
        var obj = state.Parse(rule);
        if (obj is not null)
        {
            state.ErrorCollector.Finish(state.Mark());
        }
        if (!state.Scope.HasOnlySingleFrame())
        {
            throw new InvalidOperationException("Malformed scope: " + state.Scope);
        }
        return Optional<T>.OfNullable(obj);
    }
}
