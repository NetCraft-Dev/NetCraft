using NetCraft.Commands.Arguments;
using NetCraft.Commands.Builder;
using NetCraft.Commands.Context;
using NetCraft.Commands.Exceptions;
using NetCraft.Commands.Suggestion;

namespace NetCraft.Commands.Tree;

//ArgumentCommandNode abstract base class, maps to vanilla ArgumentCommandNode<S,?>
//The CommandNode.arguments index stores this base class to avoid the generic T erasure problem
//The derived ArgumentCommandNode<S,T> holds a strongly typed ArgumentType<T>
public abstract class ArgumentCommandNode<S> : CommandNode<S>
{
    protected ArgumentCommandNode(Command<S>? command, Predicate<S> requirement, CommandNode<S>? redirect, RedirectModifier<S>? modifier, bool forks)
        : base(command, requirement, redirect, modifier, forks)
    {
    }

    public abstract string Name { get; }
    public abstract SuggestionProvider<S>? CustomSuggestions { get; }
    public abstract Task<Suggestions> ListSuggestionsCore(CommandContext<S> context, SuggestionsBuilder builder);
    public abstract bool IsValidInputCore(string input);
    public abstract IReadOnlyList<string> ExamplesCore { get; }

    //GetArgumentTypeObject weakly typed access to the argument type instance; the network layer uses it to look up ArgumentTypeInfo
    //ArgumentType<T> has no common base class, so command tree serialization can only carry it as object
    public abstract object GetArgumentTypeObject();

    public override string GetName() => Name;
}

//ArgumentCommandNode<S,T> strongly typed argument node, maps to vanilla com.mojang.brigadier.tree.ArgumentCommandNode<S,T>
//Holds an ArgumentType<T> and calls type.Parse to parse the argument into the CommandContextBuilder
public sealed class ArgumentCommandNode<S, T> : ArgumentCommandNode<S>
{
    private const string USAGE_ARGUMENT_OPEN = "<";
    private const string USAGE_ARGUMENT_CLOSE = ">";

    private readonly string _name;
    private readonly ArgumentType<T> _type;
    private readonly SuggestionProvider<S>? _customSuggestions;

    public ArgumentCommandNode(string name, ArgumentType<T> type, Command<S>? command, Predicate<S> requirement, CommandNode<S>? redirect, RedirectModifier<S>? modifier, bool forks, SuggestionProvider<S>? customSuggestions)
        : base(command, requirement, redirect, modifier, forks)
    {
        _name = name;
        _type = type;
        _customSuggestions = customSuggestions;
    }

    public ArgumentType<T> GetArgumentType() => _type;

    public override object GetArgumentTypeObject() => _type;

    public override string Name => _name;

    public override SuggestionProvider<S>? CustomSuggestions => _customSuggestions;

    public override string GetUsageText() => USAGE_ARGUMENT_OPEN + _name + USAGE_ARGUMENT_CLOSE;

    public override void Parse(StringReader reader, CommandContextBuilder<S> contextBuilder)
    {
        var start = reader.Cursor;
        var result = _type.Parse(reader);
        var parsed = new ParsedArgument<S, T>(start, reader.Cursor, result);

        contextBuilder.WithArgument(_name, parsed);
        contextBuilder.WithNode(this, parsed.Range);
    }

    public override Task<Suggestions> ListSuggestions(CommandContext<S> context, SuggestionsBuilder builder)
    {
        if (_customSuggestions == null)
        {
            return _type.ListSuggestions(context, builder);
        }
        return _customSuggestions(context, builder);
    }

    public override Task<Suggestions> ListSuggestionsCore(CommandContext<S> context, SuggestionsBuilder builder)
        => ListSuggestions(context, builder);

    public override RequiredArgumentBuilder<S, T> CreateBuilder()
    {
        var builder = RequiredArgumentBuilder<S, T>.Argument(_name, _type);
        builder.Requires(GetRequirement());
        builder.Forward(GetRedirect(), GetRedirectModifier(), IsFork());
        builder.Suggests(_customSuggestions);
        if (GetCommand() != null)
        {
            builder.Executes(GetCommand()!);
        }
        return builder;
    }

    protected override bool IsValidInput(string input)
    {
        try
        {
            var reader = new StringReader(input);
            _type.Parse(reader);
            return !reader.CanRead() || reader.Peek() == ' ';
        }
        catch (CommandSyntaxException)
        {
            return false;
        }
    }

    public override bool IsValidInputCore(string input) => IsValidInput(input);

    public override bool Equals(object? o)
    {
        if (ReferenceEquals(this, o)) return true;
        if (o is not ArgumentCommandNode<S> that) return false;
        if (!Name.Equals(that.Name)) return false;
        if (!ArgumentTypeEquals(that)) return false;
        return base.Equals(o);
    }

    //ArgumentTypeEquals checks ArgumentType<T> equality under type erasure; both sides must be ArgumentCommandNode<S,T>
    private bool ArgumentTypeEquals(ArgumentCommandNode<S> other)
    {
        if (other is not ArgumentCommandNode<S, T> typed) return false;
        return _type.Equals(typed._type);
    }

    public override int GetHashCode()
    {
        var result = _name.GetHashCode();
        result = 31 * result + _type.GetHashCode();
        return result;
    }

    protected override string GetSortedKey() => _name;

    public override IReadOnlyCollection<string> GetExamples() => _type.Examples;

    public override IReadOnlyList<string> ExamplesCore => _type.Examples;

    public override string ToString() => $"<argument {_name}:{_type}>";
}
