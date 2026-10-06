using NetCraft.Commands.Tree;

namespace NetCraft.Commands.Builder;

//ArgumentBuilder command builder abstract base class, maps to vanilla com.mojang.brigadier.builder.ArgumentBuilder
//The non-generic base lets createBuilder return ArgumentBuilder<S>, mirroring Java's wildcard ArgumentBuilder<S,?>
//Holds the arguments RootCommandNode and the command/requirement/target/modifier/forks fields
public abstract class ArgumentBuilder<S>
{
    protected readonly RootCommandNode<S> _arguments = new();
    protected Command<S>? _command;
    protected Predicate<S> _requirement = _ => true;
    protected CommandNode<S>? _target;
    protected RedirectModifier<S>? _modifier;
    protected bool _forks;

    public IReadOnlyCollection<CommandNode<S>> GetArguments() => _arguments.GetChildren();

    public Command<S>? GetCommand() => _command;

    public Predicate<S> GetRequirement() => _requirement;

    public CommandNode<S>? GetRedirect() => _target;

    public RedirectModifier<S>? GetRedirectModifier() => _modifier;

    public bool IsFork() => _forks;

    //Requires overrides the permission predicate; maps to vanilla ArgumentBuilder<S,?>.requires
    //Generic subclasses hide it with new and return the concrete builder type
    public void Requires(Predicate<S> requirement) => _requirement = requirement;

    //Forward overrides the redirect; maps to vanilla ArgumentBuilder<S,?>.forward
    public void Forward(CommandNode<S>? target, RedirectModifier<S>? modifier, bool fork)
    {
        if (_arguments.GetChildren().Count > 0)
        {
            throw new InvalidOperationException("Cannot forward a node with children");
        }
        _target = target;
        _modifier = modifier;
        _forks = fork;
    }

    public abstract CommandNode<S> Build();
}

//ArgumentBuilder<S,T> self-referential generic builder, maps to vanilla ArgumentBuilder<S,T extends ArgumentBuilder<S,T>>
//T is the concrete subclass type so chained methods like Then/Executes return T and stay type-safe
public abstract class ArgumentBuilder<S, T> : ArgumentBuilder<S> where T : ArgumentBuilder<S, T>
{
    protected abstract T GetThis();

    protected T This => GetThis();

    public T Then(ArgumentBuilder<S> argument)
    {
        if (_target != null)
        {
            throw new InvalidOperationException("Cannot add children to a redirected node");
        }
        _arguments.AddChild(argument.Build());
        return GetThis();
    }

    public T Then(CommandNode<S> argument)
    {
        if (_target != null)
        {
            throw new InvalidOperationException("Cannot add children to a redirected node");
        }
        _arguments.AddChild(argument);
        return GetThis();
    }

    public T Executes(Command<S> command)
    {
        _command = command;
        return GetThis();
    }

    public new T Requires(Predicate<S> requirement)
    {
        _requirement = requirement;
        return GetThis();
    }

    public T Redirect(CommandNode<S> target)
    {
        return Forward(target, null, false);
    }

    public T Redirect(CommandNode<S> target, SingleRedirectModifier<S>? modifier)
    {
        RedirectModifier<S>? redirectModifier = null;
        if (modifier != null)
        {
            redirectModifier = ctx => new[] { modifier(ctx) };
        }
        return Forward(target, redirectModifier, false);
    }

    public T Fork(CommandNode<S> target, RedirectModifier<S>? modifier)
    {
        return Forward(target, modifier, true);
    }

    public new T Forward(CommandNode<S>? target, RedirectModifier<S>? modifier, bool fork)
    {
        base.Forward(target, modifier, fork);
        return GetThis();
    }
}
