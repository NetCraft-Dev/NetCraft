using NetCraft.Commands.Context;

namespace NetCraft.Commands.Suggestion;

//SuggestionProvider 建议提供者委托对应原版com.mojang.brigadier.suggestion.SuggestionProvider
//参数节点持有此委托实现自定义补全
public delegate Task<Suggestions> SuggestionProvider<S>(CommandContext<S> context, SuggestionsBuilder builder);
