namespace NetCraft.Util.Parsing.Packrat;

//建议值提供者对应原版net.minecraft.util.parsing.packrat.SuggestionSupplier
//解析失败时返回可能的候选值用于命令补全
public delegate IEnumerable<string> SuggestionSupplier<S>(ParseState<S> state);

public static class SuggestionSuppliers
{
    //empty返回空候选对应原版SuggestionSupplier.empty
    public static SuggestionSupplier<S> Empty<S>() => _ => Enumerable.Empty<string>();
}
