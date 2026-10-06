namespace NetCraft.Util.Parsing.Packrat;

//Rule interface, maps to vanilla net.minecraft.util.parsing.packrat.Rule
//parse consumes ParseState and returns T, or null to indicate failure
public interface Rule<S, T>
{
    T Parse(ParseState<S> state);
}

//Rule action, maps to vanilla Rule.RuleAction
//Called after a sub-item parses successfully to produce T
public delegate T RuleAction<S, out T>(ParseState<S> state);

//Simple rule action, maps to vanilla Rule.SimpleRuleAction
//Depends only on Scope, no direct access to state
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
    //fromTerm builds a Rule using child as the sub-item and action as the success action
    public static Rule<S, T> FromTerm<S, T>(Term<S> child, RuleAction<S, T> action)
        => new WrappedTerm<S, T>(action, child);

    //fromTerm overload accepts a SimpleRuleAction, converting to RuleAction internally
    public static Rule<S, T> FromTerm<S, T>(Term<S> child, SimpleRuleAction<S, T> action)
        => new WrappedTerm<S, T>(state => action(state.Scope), child);
}
