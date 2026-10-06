namespace NetCraft.Util.Parsing.Packrat;

//Suggestion supplier, maps to vanilla net.minecraft.util.parsing.packrat.SuggestionSupplier
//On parse failure returns possible candidates for command completion
public delegate IEnumerable<string> SuggestionSupplier<S>(ParseState<S> state);

public static class SuggestionSuppliers
{
    //empty returns empty candidates, maps to vanilla SuggestionSupplier.empty
    public static SuggestionSupplier<S> Empty<S>() => _ => Enumerable.Empty<string>();
}
