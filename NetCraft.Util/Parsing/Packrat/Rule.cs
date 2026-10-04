namespace NetCraft.Util.Parsing.Packrat;

//规则接口对应原版net.minecraft.util.parsing.packrat.Rule
//parse消费ParseState返回T或null表示失败
public interface Rule<S, T>
{
    T Parse(ParseState<S> state);
}

//规则动作对应原版Rule.RuleAction
//在子项解析成功后调用产出T
public delegate T RuleAction<S, out T>(ParseState<S> state);

//简单规则动作对应原版Rule.SimpleRuleAction
//只依赖Scope不直接访问state
public delegate T SimpleRuleAction<S, out T>(Scope ruleScope);

public sealed class WrappedTerm<S, T> : Rule<S, T>
{
    public RuleAction<S, T> Action { get; }
    public Term<S> Child { get; }

    public WrappedTerm(RuleAction<S, T> action, Term<S> child)
    {
        Action = action;
        Child = child;
    }

    public T Parse(ParseState<S> state)
    {
        var scope = state.Scope;
        scope.PushFrame();
        try
        {
            if (Child.Parse(state, scope, UnboundControl.Instance))
            {
                return Action(state);
            }
            return default!;
        }
        finally
        {
            scope.PopFrame();
        }
    }
}

public static class Rules
{
    //fromTerm用child作为子项action作为成功后动作构造Rule
    public static Rule<S, T> FromTerm<S, T>(Term<S> child, RuleAction<S, T> action)
        => new WrappedTerm<S, T>(action, child);

    //fromTerm重载接受SimpleRuleAction内部转RuleAction
    public static Rule<S, T> FromTerm<S, T>(Term<S> child, SimpleRuleAction<S, T> action)
        => new WrappedTerm<S, T>(state => action(state.Scope), child);
}
