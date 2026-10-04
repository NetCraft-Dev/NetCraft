namespace NetCraft.Gpu;

//GuiControl GUI 控件基类
//提供 bounds/visible/enabled/parent + 鼠标键盘事件 + Render 抽象
//实现 ILayoutElement 适配原版 Layout 体系 X/Y/Width/Height 直接满足接口
public abstract class GuiControl : IDisposable, ILayoutElement
{
    private GuiRectangle _bounds;
    private bool _visible = true;
    private bool _enabled = true;
    private bool _disposed;
    //_isDirty 默认 true 首次 Render 必须录制 cache 之后 ClearDirtyTree 清 false
    //属性 setter 调 MarkDirty 向上传播父链任意祖先 dirty 整子树重 Render
    internal bool _isDirty = true;
    //_renderCache 控件级 RenderState 缓存未 dirty 时 ReplayRange 重放跳过 Render
    //GuiContainer 属性变化时 MarkDirty 向下传播子控件 cache 失效重 Render
    protected List<GuiElementRenderState>? _renderCache;

    //IsDirty 控件自身或子树需要重新 Render 录制新 cache
    public bool IsDirty => _isDirty;

    public int X { get => _bounds.X; set { _bounds = new GuiRectangle(value, _bounds.Y, _bounds.Width, _bounds.Height); MarkDirty(); } }
    public int Y { get => _bounds.Y; set { _bounds = new GuiRectangle(_bounds.X, value, _bounds.Width, _bounds.Height); MarkDirty(); } }
    public int Width { get => _bounds.Width; set { _bounds = new GuiRectangle(_bounds.X, _bounds.Y, value, _bounds.Height); MarkDirty(); } }
    public int Height { get => _bounds.Height; set { _bounds = new GuiRectangle(_bounds.X, _bounds.Y, _bounds.Width, value); MarkDirty(); } }

    public GuiRectangle Bounds
    {
        get => _bounds;
        set { _bounds = value; MarkDirty(); }
    }

    public bool Visible
    {
        get => _visible;
        set { _visible = value; MarkDirty(); }
    }

    public bool Enabled
    {
        get => _enabled;
        set { _enabled = value; MarkDirty(); }
    }

    //MarkDirty 标记自身 dirty 并向上传播父链遇到已 dirty 祖先停止
    //祖先已 dirty 意味着整树会重 Render 子树无需重复标记
    //GuiContainer override 额外向下传播子控件 cache 失效
    protected virtual void MarkDirty()
    {
        var c = this;
        while (c is not null && !c._isDirty)
        {
            c._isDirty = true;
            c = c.Parent;
        }
    }

    //MarkDirtyDown 向下传播 dirty 到子控件 GuiContainer override 递归子树
    //leaf 控件默认实现只标记自身 dirty 已 dirty 跳过避免重复标记
    internal virtual void MarkDirtyDown()
    {
        if (_isDirty) return;
        _isDirty = true;
    }

    //ClearDirtyTree Render 录制后清自身 dirty GuiContainer override 递归清子控件
    internal virtual void ClearDirtyTree() => _isDirty = false;

    //TabStop 是否参与 Tab 键焦点导航默认 false 控件设 true 才能被 Tab 聚焦
    public bool TabStop { get; set; }

    //TabIndex Tab 键导航顺序默认 0 值小的先聚焦同值按控件树顺序
    public int TabIndex { get; set; }

    public GuiContainer? Parent { get; internal set; }

    public bool ContainsPoint(int x, int y) => _bounds.Contains(x, y);

    public event EventHandler<MouseEventArgs>? MouseDown;
    public event EventHandler<MouseEventArgs>? MouseUp;
    public event EventHandler<MouseEventArgs>? MouseClick;
    public event EventHandler<MouseEventArgs>? MouseMove;
    public event EventHandler<MouseEventArgs>? MouseEnter;
    public event EventHandler<MouseEventArgs>? MouseLeave;
    public event EventHandler<KeyEventArgs>? KeyDown;
    public event EventHandler<KeyEventArgs>? KeyUp;
    public event EventHandler<KeyEventArgs>? KeyPress;

    //Render 由控件子类实现绘制自身
    public abstract void Render(IGuiRenderContext context);

    //RenderWithCache retained mode 入口未 dirty 时 ReplayRange 重放跳过 Render
    //dirty 时 BeginRecording 录制 Render 期间 Submit 的 RenderState 到 _renderCache
    //录制栈支持嵌套父控件 cache 和子控件 cache 同时活跃 Submit 写入栈所有层
    //blur 帧由 GuiWindow 强制不走 cache 确保 BlurBeforeThisStratum 每帧重新调
    public void RenderWithCache(IGuiRenderContext context)
    {
        if (!_isDirty && _renderCache is not null)
        {
            context.ReplayRange(_renderCache);
            return;
        }
        _renderCache ??= new();
        _renderCache.Clear();
        context.BeginRecording(_renderCache);
        Render(context);
        context.EndRecording();
        ClearDirtyTree();
    }

    //Update 每帧调用推进动画状态子类可重写自定义行为
    //delta 单帧时长秒数对应原版 partialTick
    public virtual void Update(double delta) { }

    //OnMouseDown 派发鼠标按下事件子类可重写自定义行为
    protected internal virtual void OnMouseDown(MouseEventArgs e) => MouseDown?.Invoke(this, e);
    protected internal virtual void OnMouseUp(MouseEventArgs e) => MouseUp?.Invoke(this, e);
    protected internal virtual void OnMouseClick(MouseEventArgs e) => MouseClick?.Invoke(this, e);
    protected internal virtual void OnMouseMove(MouseEventArgs e) => MouseMove?.Invoke(this, e);
    protected internal virtual void OnMouseEnter(MouseEventArgs e) => MouseEnter?.Invoke(this, e);
    protected internal virtual void OnMouseLeave(MouseEventArgs e) => MouseLeave?.Invoke(this, e);
    protected internal virtual void OnKeyDown(KeyEventArgs e) => KeyDown?.Invoke(this, e);
    protected internal virtual void OnKeyUp(KeyEventArgs e) => KeyUp?.Invoke(this, e);
    protected internal virtual void OnKeyPress(KeyEventArgs e) => KeyPress?.Invoke(this, e);

    //OnGotFocus/OnLostFocus 焦点切换通知由 GuiWindow 在设 _focusedControl 时调用
    //TextBox override 启停光标闪烁
    protected internal virtual void OnGotFocus() { }
    protected internal virtual void OnLostFocus() { }

    public virtual void Dispose() => _disposed = true;
}
