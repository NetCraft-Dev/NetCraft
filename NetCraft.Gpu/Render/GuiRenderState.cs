namespace NetCraft.Gpu;

//GuiRenderState RenderState 容器对标原版 GuiRenderState
//管理 strata 横向分层和 up 纵向层级保证 z 顺序
//每帧 Reset 不跨帧
public sealed class GuiRenderState
{
    private Node? _current;
    private ScreenRectangle? _lastElementBounds;
    private readonly List<Node> _strata = new();
    private int _firstStratumAfterBlur = int.MaxValue;

    public GuiRenderState() : this(skipInit: false) { }

    //Snapshot 深拷贝 strata+Node 树供 Render 线程只读
    //元素 record 不可变引用共享只复制 List 容器和 Node 结构
    private GuiRenderState(bool skipInit)
    {
        if (!skipInit) NextStratum();
    }

    //Snapshot 产出不可变快照 Tick 写完后调 Render 线程只读
    public GuiRenderState Snapshot()
    {
        var copy = new GuiRenderState(skipInit: true);
        foreach (var node in _strata) copy._strata.Add(CloneNode(node));
        copy._firstStratumAfterBlur = _firstStratumAfterBlur;
        return copy;
    }

    //CloneNode 重建 Node 的 Up 链和 ElementStates/GlyphStates/PipStates 列表
    private static Node CloneNode(Node src)
    {
        var dst = new Node(src.Parent);
        if (src.Up is not null) dst.Up = CloneNode(src.Up);
        if (src.ElementStates is not null)
            dst.ElementStates = new List<GuiElementRenderState>(src.ElementStates);
        if (src.GlyphStates is not null)
            dst.GlyphStates = new List<GuiElementRenderState>(src.GlyphStates);
        if (src.PipStates is not null)
            dst.PipStates = new List<PictureInPictureRenderState>(src.PipStates);
        return dst;
    }

    //NextStratum 开启新 stratum 横向分层背景层/内容层/覆盖层典型
    public void NextStratum()
    {
        _current = new Node(null);
        _strata.Add(_current);
    }

    //HasBlurSplit 是否调用了 BlurBeforeThisStratum 需要 Prepare 分段+Draw 分段执行
    public bool HasBlurSplit => _firstStratumAfterBlur != int.MaxValue;

    //BlurBeforeThisStratum 标记当前 stratum 之前为 blur 前段一帧仅可调一次
    public void BlurBeforeThisStratum()
    {
        if (_firstStratumAfterBlur != int.MaxValue)
            throw new InvalidOperationException("Can only blur once per frame");
        _firstStratumAfterBlur = _strata.Count - 1;
    }

    //Up 在当前 Node 之上创建子节点 current 切到子节点保证后续元素绘制在上层
    public void Up()
    {
        if (_current!.Up == null)
            _current.Up = new Node(_current);
        _current = _current.Up;
    }

    //AddGuiElement 添加普通元素经过 FindAppropriateNode 自动定位层级
    public void AddGuiElement(GuiElementRenderState element)
    {
        FindAppropriateNode(element);
        _current!.AddElement(element);
    }

    //AddGlyphToCurrentLayer 直接添加字形到当前层不参与 bounds 树
    //字形 Bounds 由字形纹理自身决定不参与层级相交判断
    public void AddGlyphToCurrentLayer(GuiElementRenderState glyph)
    {
        _current!.AddGlyph(glyph);
    }

    //AddPictureInPicture 添加 PIP 状态到当前层对标原版 addPicturesInPictureState
    //PIP 不参与 bounds 树层级判断由 PictureInPictureRenderer 在 Prepare 阶段 offscreen 渲染后 blit
    public void AddPictureInPicture(PictureInPictureRenderState pip)
    {
        _current!.AddPip(pip);
    }

    //ForEachPictureInPicture 遍历所有 strata 的 pipStates 对标原版 forEachPictureInPicture
    //PIP 不参与 blur 分段遍历全部 strata Prepare 阶段调 PictureInPictureRenderer.prepare
    public void ForEachPictureInPicture(Action<PictureInPictureRenderState> visitor)
    {
        foreach (var node in _strata)
            TraversePip(node, visitor);
    }

    private static void TraversePip(Node node, Action<PictureInPictureRenderState> visitor)
    {
        if (node.PipStates != null)
            foreach (var pip in node.PipStates) visitor(pip);
        if (node.Up != null) TraversePip(node.Up, visitor);
    }

    //ForEachElement 按 range 遍历元素深度优先访问 elementStates + glyphStates
    public void ForEachElement(Action<GuiElementRenderState> visitor, TraverseRange range)
    {
        Traverse(node =>
        {
            if (node.ElementStates == null && node.GlyphStates == null) return;
            if (node.ElementStates != null)
                foreach (var e in node.ElementStates) visitor(e);
            if (node.GlyphStates != null)
                foreach (var g in node.GlyphStates) visitor(g);
        }, range);
    }

    //SortElements 按 comparator 对每个 Node 的 elementStates 排序 glyphStates 不参与
    public void SortElements(Comparison<GuiElementRenderState> comparison)
    {
        Traverse(node =>
        {
            if (node.ElementStates != null)
                node.ElementStates.Sort(comparison);
        }, TraverseRange.All);
    }

    //Reset 清空所有状态并开启首个 stratum 每帧调用
    public void Reset()
    {
        _strata.Clear();
        _firstStratumAfterBlur = int.MaxValue;
        _lastElementBounds = null;
        NextStratum();
    }

    //FindAppropriateNode 找到 element 应插入的 Node
    //Bounds 非 nullable 永远继续若 lastElementBounds 包含当前 bounds 则 Up 否则向上找相交节点
    private void FindAppropriateNode(GuiElementRenderState element)
    {
        var bounds = element.Bounds;
        if (_lastElementBounds is { } lastBounds && lastBounds.Encompasses(bounds))
        {
            Up();
        }
        else
        {
            NavigateToAboveHighestElementWithIntersectingBounds(bounds);
        }
        _lastElementBounds = bounds;
    }

    //NavigateToAboveHighestElementWithIntersectingBounds 从 strata 栈顶向上找最高相交节点
    //找到则 current = 相交节点后 Up 在其之上加新层未找到则 current = root
    private void NavigateToAboveHighestElementWithIntersectingBounds(ScreenRectangle bounds)
    {
        var node = _strata[^1];
        while (node.Up != null)
            node = node.Up;

        bool found = false;
        while (!found)
        {
            found = HasIntersection(bounds, node.ElementStates)
                || HasIntersection(bounds, node.GlyphStates);
            if (node.Parent == null) break;
            if (!found) node = node.Parent;
        }

        _current = node;
        if (found) Up();
    }

    private static bool HasIntersection(ScreenRectangle bounds, List<GuiElementRenderState>? states)
    {
        if (states == null) return false;
        foreach (var s in states)
            if (s.Bounds.Intersects(bounds)) return true;
        return false;
    }

    private void Traverse(Action<Node> visitor, TraverseRange range)
    {
        int start = 0;
        int end = _strata.Count;
        if (range == TraverseRange.BeforeBlur)
            end = Math.Min(_firstStratumAfterBlur, _strata.Count);
        else if (range == TraverseRange.AfterBlur)
            start = _firstStratumAfterBlur;

        for (int i = start; i < end; i++)
            Traverse(_strata[i], visitor);
    }

    private static void Traverse(Node node, Action<Node> visitor)
    {
        visitor(node);
        if (node.Up != null) Traverse(node.Up, visitor);
    }

    //Node 内部层级节点懒初始化各状态列表避免空容器内存开销
    private sealed class Node
    {
        public readonly Node? Parent;
        public Node? Up;
        public List<GuiElementRenderState>? ElementStates;
        public List<GuiElementRenderState>? GlyphStates;
        //PipStates PIP 渲染状态列表懒初始化对标原版 picturesInPictureStates
        public List<PictureInPictureRenderState>? PipStates;

        public Node(Node? parent) => Parent = parent;

        public void AddElement(GuiElementRenderState element)
        {
            ElementStates ??= new List<GuiElementRenderState>();
            ElementStates.Add(element);
        }

        public void AddGlyph(GuiElementRenderState glyph)
        {
            GlyphStates ??= new List<GuiElementRenderState>();
            GlyphStates.Add(glyph);
        }

        public void AddPip(PictureInPictureRenderState pip)
        {
            PipStates ??= new List<PictureInPictureRenderState>();
            PipStates.Add(pip);
        }
    }
}
