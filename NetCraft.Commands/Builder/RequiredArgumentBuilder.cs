using NetCraft.Commands.Arguments;
using NetCraft.Commands.Suggestion;
using NetCraft.Commands.Tree;

namespace NetCraft.Commands.Builder;

//RequiredArgumentBuilder 必需参数构建器对应原版com.mojang.brigadier.builder.RequiredArgumentBuilder
//链式构建ArgumentCommandNode<S,T>静态Argument方法创建实例
//持name与ArgumentType<T>SuggestionProvider可选自定义补全
public sealed class RequiredArgumentBuilder<S, T> : ArgumentBuilder<S, RequiredArgumentBuilder<S, T>>
{
    private readonly string _name;
    private readonly ArgumentType<T> _type;
    private SuggestionProvider<S>? _suggestionsProvider;

    private RequiredArgumentBuilder(string name, ArgumentType<T> type)
    {
        _name = name;
        _type = type;
    }

    public static RequiredArgumentBuilder<S, T> Argument(string name, ArgumentType<T> type) => new(name, type);

    public RequiredArgumentBuilder<S, T> Suggests(SuggestionProvider<S>? provider)
    {
        _suggestionsProvider = provider;
        return this;
    }

    public SuggestionProvider<S>? GetSuggestionsProvider() => _suggestionsProvider;

    protected override RequiredArgumentBuilder<S, T> GetThis() => this;

    public ArgumentType<T> GetArgumentType() => _type;

    public string GetName() => _name;

    public override ArgumentCommandNode<S, T> Build()
    {
        var result = new ArgumentCommandNode<S, T>(_name, _type, GetCommand(), GetRequirement(), GetRedirect(), GetRedirectModifier(), IsFork(), _suggestionsProvider);

        foreach (var argument in GetArguments())
        {
            result.AddChild(argument);
        }

        return result;
    }
}
