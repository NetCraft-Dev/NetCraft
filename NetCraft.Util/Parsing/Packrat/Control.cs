namespace NetCraft.Util.Parsing.Packrat;

//Parse control signal, maps to vanilla net.minecraft.util.parsing.packrat.Control
//cut marks a rule as failed early, hasCut queries whether already cut
public interface Control
{
    void Cut();

    bool HasCut();
}

//UNBOUND empty implementation for default scenarios
public sealed class UnboundControl : Control
{
    public static UnboundControl Instance { get; } = new();

    private UnboundControl() { }

    public void Cut() { }

    public bool HasCut() => false;
}
