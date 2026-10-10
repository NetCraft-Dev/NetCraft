namespace NetCraft.Hook;

public sealed class HookResult
{
    public byte[] RewrittenAssembly { get; }
    public int RewritesPerformed { get; }
    public int RulesApplied { get; }
    public bool HasRewrites => RewritesPerformed > 0;

    internal HookResult(byte[] rewrittenAssembly, int rewritesPerformed, int rulesApplied)
    {
        RewrittenAssembly = rewrittenAssembly;
        RewritesPerformed = rewritesPerformed;
        RulesApplied = rulesApplied;
    }
}
