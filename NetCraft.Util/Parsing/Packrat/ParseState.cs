using NetCraft.Codec;

namespace NetCraft.Util.Parsing.Packrat;

//Parse state, maps to vanilla net.minecraft.util.parsing.packrat.ParseState
//Holds scope and errorCollector, provides the rule parse entry and cursor mark/restore
//silent returns a state that collects no errors
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
    //parseTopRule parses the top-level rule and returns an Optional result
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
