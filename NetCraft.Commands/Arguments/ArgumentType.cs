using NetCraft.Commands.Context;
using NetCraft.Commands.Suggestion;

namespace NetCraft.Commands.Arguments;

//ArgumentType 参数类型接口对应原版com.mojang.brigadier.arguments.ArgumentType
//所有参数解析器实现Parse从StringReader读取值ListSuggestions提供补全
public interface ArgumentType<T>
{
    T Parse(StringReader reader);

    //Parse 带source重载默认转发到无source版本
    T Parse<S>(StringReader reader, S source) => Parse(reader);

    //ListSuggestions 默认返回空建议
    Task<Suggestions> ListSuggestions<S>(CommandContext<S> context, SuggestionsBuilder builder)
        => Suggestions.Empty();

    IReadOnlyList<string> Examples => Array.Empty<string>();
}
