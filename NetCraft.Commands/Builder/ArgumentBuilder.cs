using NetCraft.Commands.Tree;

namespace NetCraft.Commands.Builder;

//ArgumentBuilder 命令构建器抽象基类对应原版com.mojang.brigadier.builder.ArgumentBuilder
//非泛型基类让createBuilder返回ArgumentBuilder<S>对应Java通配ArgumentBuilder<S,?>
//持arguments RootCommandNode与command/requirement/target/modifier/forks字段
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

    //Requires 覆写权限谓词 对应原版 ArgumentBuilder<S,?>.requires
    //泛型派生类以 new 隐藏并返回具体构建器类型
    public void Requires(Predicate<S> requirement) => _requirement = requirement;

    //Forward 覆写重定向 对应原版 ArgumentBuilder<S,?>.forward
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

//ArgumentBuilder<S,T> 自递归泛型构建器对应原版ArgumentBuilder<S,T extends ArgumentBuilder<S,T>>
//T表示具体子类类型让Then/Executes等链式方法返回T保证类型安全
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
