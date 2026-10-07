namespace NetCraft.Gpu;

//GuiControl GUI widget base class
//Provides bounds/visible/enabled/parent + mouse and keyboard events + the Render abstraction
//Implements ILayoutElement to fit the vanilla Layout system; X/Y/Width/Height satisfy the interface directly
public abstract class GuiControl : IDisposable, ILayoutElement
{
    private GuiRectangle _bounds;
    private bool _visible = true;
    private bool _enabled = true;
    private bool _disposed;
    //_isDirty defaults to true; the first Render must record cache, then ClearDirtyTree clears it to false
    //Property setters call MarkDirty and propagate up the parent chain; any dirty ancestor re-Renders the whole subtree
    internal bool _isDirty = true;
    //_renderCache widget-level RenderState cache; when not dirty, ReplayRange replays and skips Render
    //When a GuiContainer property changes, MarkDirty propagates down, invalidating child caches and forcing a re-Render
    protected List<GuiElementRenderState>? _renderCache;

    //IsDirty the widget itself or its subtree needs a re-Render to record a new cache
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

    //MarkDirty marks itself dirty and propagates up the parent chain, stopping at an already-dirty ancestor
    //A dirty ancestor means the whole tree will re-Render, so the subtree needs no further marking
    //GuiContainer override additionally propagates down to invalidate child caches
    protected virtual void MarkDirty()
    {
        var c = this;
        while (c is not null && !c._isDirty)
        {
            c._isDirty = true;
            c = c.Parent;
        }
    }

    //MarkDirtyDown propagates dirty down to children; GuiContainer override recurses the subtree
    //The default leaf implementation only marks itself dirty, skipping already-dirty ones to avoid redundant marking
    internal virtual void MarkDirtyDown()
    {
        if (_isDirty) return;
        _isDirty = true;
    }

    //ClearDirtyTree clears its own dirty after a Render recording; GuiContainer override recurses into children
    internal virtual void ClearDirtyTree() => _isDirty = false;

    //TabStop whether the widget participates in Tab focus navigation, default false; only widgets set to true can be Tab-focused
    public bool TabStop { get; set; }

    //TabIndex Tab navigation order, default 0; smaller values focus first, ties follow widget tree order
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

    //Render implemented by subclasses to draw themselves
    public abstract void Render(IGuiRenderContext context);

    //RenderWithCache retained-mode entry; when not dirty it ReplayRanges and skips Render
    //When dirty it BeginRecords the RenderStates submitted during Render into _renderCache
    //The recording stack supports nested parent and child caches active at once; Submit writes to all stack levels
    //Blur frames are forced by GuiWindow to skip the cache so BlurBeforeThisStratum is called every frame
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

    //Update called every frame to advance animation state; subclasses may override custom behavior
    //delta single-frame duration in seconds, corresponds to vanilla partialTick
    public virtual void Update(double delta) { }

    //OnMouseDown dispatches the mouse-down event; subclasses may override custom behavior
    protected internal virtual void OnMouseDown(MouseEventArgs e) => MouseDown?.Invoke(this, e);
    protected internal virtual void OnMouseUp(MouseEventArgs e) => MouseUp?.Invoke(this, e);
    protected internal virtual void OnMouseClick(MouseEventArgs e) => MouseClick?.Invoke(this, e);
    protected internal virtual void OnMouseMove(MouseEventArgs e) => MouseMove?.Invoke(this, e);
    protected internal virtual void OnMouseEnter(MouseEventArgs e) => MouseEnter?.Invoke(this, e);
    protected internal virtual void OnMouseLeave(MouseEventArgs e) => MouseLeave?.Invoke(this, e);
    protected internal virtual void OnKeyDown(KeyEventArgs e) => KeyDown?.Invoke(this, e);
    protected internal virtual void OnKeyUp(KeyEventArgs e) => KeyUp?.Invoke(this, e);
    protected internal virtual void OnKeyPress(KeyEventArgs e) => KeyPress?.Invoke(this, e);

    //OnGotFocus/OnLostFocus focus-change notifications, called by GuiWindow when setting _focusedControl
    //TextBox override starts/stops caret blinking
    protected internal virtual void OnGotFocus() { }
    protected internal virtual void OnLostFocus() { }

    public virtual void Dispose() => _disposed = true;
}
