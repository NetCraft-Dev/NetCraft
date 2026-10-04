namespace NetCraft.Util.Parsing.Packrat;

//解析项对应原版net.minecraft.util.parsing.packrat.Term
//parse返回是否成功通过scope和control与上层交互
public interface Term<S>
{
    bool Parse(ParseState<S> state, Scope scope, Control control);
}

public static class Terms
{
    //marker把固定值塞进scope对应name下
    public static Term<S> Marker<S, T>(Atom<T> name, T value)
        => new MarkerTerm<S, T>(name, value);

    //sequence按顺序解析全部成功才成功
    public static Term<S> Sequence<S>(params Term<S>[] terms)
        => new SequenceTerm<S>(terms);

    //alternative任一子项成功即成功支持cut提前剪枝
    public static Term<S> Alternative<S>(params Term<S>[] terms)
        => new AlternativeTerm<S>(terms);

    //optional子项失败也算成功
    public static Term<S> Optional<S>(Term<S> term)
        => new MaybeTerm<S>(term);

    //repeated零次或多次结果存进listName
    public static Term<S> Repeated<S, T>(NamedRule<S, T> element, Atom<List<T>> listName)
        => new RepeatedTerm<S, T>(element, listName, 0);

    public static Term<S> Repeated<S, T>(NamedRule<S, T> element, Atom<List<T>> listName, int minRepetitions)
        => new RepeatedTerm<S, T>(element, listName, minRepetitions);

    //repeatedWithTrailingSeparator允许末尾保留分隔符
    public static Term<S> RepeatedWithTrailingSeparator<S, T>(NamedRule<S, T> element, Atom<List<T>> listName, Term<S> separator)
        => new RepeatedWithSeparatorTerm<S, T>(element, listName, separator, 0, true);

    public static Term<S> RepeatedWithTrailingSeparator<S, T>(NamedRule<S, T> element, Atom<List<T>> listName, Term<S> separator, int minRepetitions)
        => new RepeatedWithSeparatorTerm<S, T>(element, listName, separator, minRepetitions, true);

    //repeatedWithoutTrailingSeparator不允许末尾分隔符
    public static Term<S> RepeatedWithoutTrailingSeparator<S, T>(NamedRule<S, T> element, Atom<List<T>> listName, Term<S> separator)
        => new RepeatedWithSeparatorTerm<S, T>(element, listName, separator, 0, false);

    public static Term<S> RepeatedWithoutTrailingSeparator<S, T>(NamedRule<S, T> element, Atom<List<T>> listName, Term<S> separator, int minRepetitions)
        => new RepeatedWithSeparatorTerm<S, T>(element, listName, separator, minRepetitions, false);

    public static Term<S> PositiveLookahead<S>(Term<S> term)
        => new LookAheadTerm<S>(term, true);

    public static Term<S> NegativeLookahead<S>(Term<S> term)
        => new LookAheadTerm<S>(term, false);

    //cut标记提前失败
    public static Term<S> Cut<S>() => new CutTerm<S>();

    //empty恒成功
    public static Term<S> Empty<S>() => new EmptyTerm<S>();

    //fail恒失败记录原因
    public static Term<S> Fail<S>(object message) => new FailTerm<S>(message);
}

public sealed class MarkerTerm<S, T> : Term<S>
{
    public Atom<T> Name { get; }
    public T Value { get; }

    public MarkerTerm(Atom<T> name, T value)
    {
        Name = name;
        Value = value;
    }

    public bool Parse(ParseState<S> state, Scope scope, Control control)
    {
        scope.Put(Name, Value);
        return true;
    }
}

public sealed class SequenceTerm<S> : Term<S>
{
    public Term<S>[] Elements { get; }

    public SequenceTerm(Term<S>[] elements) => Elements = elements;

    public bool Parse(ParseState<S> state, Scope scope, Control control)
    {
        var mark = state.Mark();
        foreach (var element in Elements)
        {
            if (!element.Parse(state, scope, control))
            {
                state.Restore(mark);
                return false;
            }
        }
        return true;
    }
}

public sealed class AlternativeTerm<S> : Term<S>
{
    public Term<S>[] Elements { get; }

    public AlternativeTerm(Term<S>[] elements) => Elements = elements;

    public bool Parse(ParseState<S> state, Scope scope, Control control)
    {
        var controlForThis = state.AcquireControl();
        try
        {
            var mark = state.Mark();
            scope.SplitFrame();
            foreach (var element in Elements)
            {
                if (element.Parse(state, scope, controlForThis))
                {
                    scope.MergeFrame();
                    state.ReleaseControl();
                    return true;
                }
                scope.ClearFrameValues();
                state.Restore(mark);
                if (controlForThis.HasCut()) break;
            }
            scope.PopFrame();
            state.ReleaseControl();
            return false;
        }
        catch
        {
            state.ReleaseControl();
            throw;
        }
    }
}

public sealed class MaybeTerm<S> : Term<S>
{
    public Term<S> Term { get; }

    public MaybeTerm(Term<S> term) => Term = term;

    public bool Parse(ParseState<S> state, Scope scope, Control control)
    {
        var mark = state.Mark();
        if (!Term.Parse(state, scope, control))
        {
            state.Restore(mark);
        }
        return true;
    }
}

public sealed class RepeatedTerm<S, T> : Term<S>
{
    public NamedRule<S, T> Element { get; }
    public Atom<List<T>> ListName { get; }
    public int MinRepetitions { get; }

    public RepeatedTerm(NamedRule<S, T> element, Atom<List<T>> listName, int minRepetitions)
    {
        Element = element;
        ListName = listName;
        MinRepetitions = minRepetitions;
    }

    public bool Parse(ParseState<S> state, Scope scope, Control control)
    {
        var mark = state.Mark();
        var list = new List<T>(MinRepetitions);
        int entryMark;
        while (true)
        {
            entryMark = state.Mark();
            var obj = state.Parse(Element);
            if (obj is null) break;
            list.Add(obj);
        }
        state.Restore(entryMark);
        if (list.Count < MinRepetitions)
        {
            state.Restore(mark);
            return false;
        }
        scope.Put(ListName, list);
        return true;
    }
}

public sealed class RepeatedWithSeparatorTerm<S, T> : Term<S>
{
    public NamedRule<S, T> Element { get; }
    public Atom<List<T>> ListName { get; }
    public Term<S> Separator { get; }
    public int MinRepetitions { get; }
    public bool AllowTrailingSeparator { get; }

    public RepeatedWithSeparatorTerm(NamedRule<S, T> element, Atom<List<T>> listName, Term<S> separator, int minRepetitions, bool allowTrailingSeparator)
    {
        Element = element;
        ListName = listName;
        Separator = separator;
        MinRepetitions = minRepetitions;
        AllowTrailingSeparator = allowTrailingSeparator;
    }

    public bool Parse(ParseState<S> state, Scope scope, Control control)
    {
        var mark = state.Mark();
        var list = new List<T>(MinRepetitions);
        var firstPass = true;
        while (true)
        {
            var separatorMark = state.Mark();
            if (!firstPass)
            {
                if (!Separator.Parse(state, scope, control))
                {
                    state.Restore(separatorMark);
                    goto checkCount;
                }
            }
            var elementMark = state.Mark();
            var obj = state.Parse(Element);
            if (obj is null)
            {
                if (firstPass)
                {
                    state.Restore(elementMark);
                    goto checkCount;
                }
                if (AllowTrailingSeparator)
                {
                    state.Restore(elementMark);
                    goto checkCount;
                }
                state.Restore(mark);
                return false;
            }
            list.Add(obj);
            firstPass = false;
        }
    checkCount:
        if (list.Count < MinRepetitions)
        {
            state.Restore(mark);
            return false;
        }
        scope.Put(ListName, list);
        return true;
    }
}

public sealed class LookAheadTerm<S> : Term<S>
{
    public Term<S> Term { get; }
    public bool Positive { get; }

    public LookAheadTerm(Term<S> term, bool positive)
    {
        Term = term;
        Positive = positive;
    }

    public bool Parse(ParseState<S> state, Scope scope, Control control)
    {
        var mark = state.Mark();
        var result = Term.Parse(state.Silent, scope, control);
        state.Restore(mark);
        return Positive == result;
    }
}

public sealed class CutTerm<S> : Term<S>
{
    public bool Parse(ParseState<S> state, Scope scope, Control control)
    {
        control.Cut();
        return true;
    }

    public override string ToString() => "↑";
}

public sealed class EmptyTerm<S> : Term<S>
{
    public bool Parse(ParseState<S> state, Scope scope, Control control) => true;

    public override string ToString() => "ε";
}

public sealed class FailTerm<S> : Term<S>
{
    private readonly object _message;

    public FailTerm(object message) => _message = message;

    public bool Parse(ParseState<S> state, Scope scope, Control control)
    {
        state.ErrorCollector.Store(state.Mark(), _message);
        return false;
    }

    public override string ToString() => "fail";
}
