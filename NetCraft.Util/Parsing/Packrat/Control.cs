namespace NetCraft.Util.Parsing.Packrat;

//解析控制信号对应原版net.minecraft.util.parsing.packrat.Control
//cut标记规则提前失败hasCut查询是否已cut
public interface Control
{
    void Cut();

    bool HasCut();
}

//UNBOUND空实现供默认场景使用
public sealed class UnboundControl : Control
{
    public static UnboundControl Instance { get; } = new();

    private UnboundControl() { }

    public void Cut() { }

    public bool HasCut() => false;
}
