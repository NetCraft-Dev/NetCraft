namespace NetCraft.Gpu;

//GuiContainer 可包含子控件的容器
//Render 默认遍历可见子控件调用其 Render
//事件派发先递归到包含点的子控件否则自己处理
public abstract class GuiContainer : GuiControl
{
    private readonly List<GuiControl> _children = new();

    public IReadOnlyList<GuiControl> Children => _children;

    //Layout 布局引擎 null 表示不自动布局子控件用自身 X/Y 定位
    public IGuiLayout? Layout { get; set; }

    public void Add(GuiControl child)
    {
        if (child.Parent is not null)
            throw new InvalidOperationException("控件已有父容器");
        child.Parent = this;
        _children.Add(child);
        MarkDirty();
    }

    public bool Remove(GuiControl child)
    {
        if (!_children.Remove(child)) return false;
        child.Parent = null;
        MarkDirty();
        return true;
    }

    public void Clear()
    {
        foreach (var child in _children) child.Parent = null;
        _children.Clear();
        MarkDirty();
    }

    //ClearDirtyTree 重 Render 录制后清自身 dirty 并递归清子控件
    //整子树都重 Render 了子控件 dirty 也清避免下帧误判
    internal override void ClearDirtyTree()
    {
        _isDirty = false;
        foreach (var child in _children)
            child.ClearDirtyTree();
    }

    //MarkDirty override 向上传播父链后向下传播所有子控件 cache 失效
    //父容器属性变化（X/Y/Width/Height/Visible 等）子控件 pose/scissor 快照过时需重 Render
    protected override void MarkDirty()
    {
        base.MarkDirty();
        foreach (var child in _children)
            child.MarkDirtyDown();
    }

    //MarkDirtyDown 向下传播 dirty 到子控件子容器递归直到 leaf
    //已 dirty 子树跳过避免重复标记
    internal override void MarkDirtyDown()
    {
        if (_isDirty) return;
        _isDirty = true;
        foreach (var c in _children)
            c.MarkDirtyDown();
    }

    public override void Render(IGuiRenderContext context)
    {
        if (!Visible) return;
        //push scissor 到容器边界裁剪子控件超出区域 GuiPanel 背景已在调用方绘制不受裁剪
        context.PushScissor(X, Y, Width, Height);
        foreach (var child in _children)
        {
            if (child.Visible) child.RenderWithCache(context);
        }
        context.PopScissor();
    }

    //Update 先 Layout.Measure 排列子控件位置再遍历可见子控件 Update 推进动画
    public override void Update(double delta)
    {
        if (!Visible) return;
        Layout?.Measure(this);
        foreach (var child in _children)
        {
            if (child.Visible) child.Update(delta);
        }
    }

    //HitTest 返回包含点的最顶层子控件层级深的优先
    public GuiControl? HitTest(int x, int y)
    {
        for (int i = _children.Count - 1; i >= 0; i--)
        {
            var child = _children[i];
            if (!child.Visible) continue;
            if (child is GuiContainer container)
            {
                var hit = container.HitTest(x, y);
                if (hit is not null) return hit;
            }
            else if (child.ContainsPoint(x, y))
            {
                return child;
            }
        }
        if (ContainsPoint(x, y)) return this;
        return null;
    }

    protected internal override void OnMouseDown(MouseEventArgs e)
    {
        var hit = HitTest(e.X, e.Y);
        if (hit is not null && hit != this)
        {
            hit.OnMouseDown(e);
        }
        else
        {
            base.OnMouseDown(e);
        }
    }

    protected internal override void OnMouseUp(MouseEventArgs e)
    {
        var hit = HitTest(e.X, e.Y);
        if (hit is not null && hit != this)
        {
            hit.OnMouseUp(e);
        }
        else
        {
            base.OnMouseUp(e);
        }
    }

    protected internal override void OnMouseMove(MouseEventArgs e)
    {
        var hit = HitTest(e.X, e.Y);
        if (hit is not null && hit != this)
        {
            hit.OnMouseMove(e);
        }
        else
        {
            base.OnMouseMove(e);
        }
    }

    public override void Dispose()
    {
        foreach (var child in _children) child.Dispose();
        _children.Clear();
        base.Dispose();
    }
}
