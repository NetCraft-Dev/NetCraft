namespace NetCraft.Gpu;

//GuiWindow 顶层 GUI 窗口
//承载控件树并对外暴露事件输入和 Render 接口
//不直接管理 Vulkan 资源由外部调用者传入 IGuiRenderContext 渲染
public sealed class GuiWindow : GuiContainer
{
    private GuiControl? _hoveredControl;
    private GuiControl? _focusedControl;
    //_shiftDown/_ctrlDown 由 ProcessKeyDown/Up 根据 Shift/Ctrl 键码维护
    //TextBox 检测 e.Modifiers 扩展选区或 Ctrl+C/V/X 组合键
    private bool _shiftDown;
    private bool _ctrlDown;

    //SurfaceWidth/Height swapchain 实际像素 scissor/pipeline extent 用
    //ScaledWidth/Height 逻辑像素 floor(Surface/GuiScale) 控件布局用
    //GuiScale 整数倍 max(1, min(SurfaceW/320, SurfaceH/240)) 控件等比放大不模糊
    public int SurfaceWidth { get; set; }
    public int SurfaceHeight { get; set; }
    public int GuiScale { get; private set; } = 1;
    public int ScaledWidth { get; private set; }
    public int ScaledHeight { get; private set; }

    private GuiColor _backgroundColor = GuiColor.FromRgb(30, 30, 30);
    //BackgroundColor 背景色 setter MarkDirty 变化时整窗口重 Render 录制新背景 quad
    public GuiColor BackgroundColor
    {
        get => _backgroundColor;
        set { _backgroundColor = value; MarkDirty(); }
    }

    //RenderBackgroundHook 屏幕级背景回调由 ScreenManager.SetScreen 注入
    //Screen 在 Game 层 Window 在 Gpu 层不能直接调通过此 hook 桥接
    //Render 时背景 quad 之后控件之前调用让 dirt 背景覆盖纯色背景
    private Action<IGuiRenderContext>? _renderBackgroundHook;
    public Action<IGuiRenderContext>? RenderBackgroundHook
    {
        get => _renderBackgroundHook;
        set { _renderBackgroundHook = value; MarkDirty(); }
    }

    //RenderForegroundHook 屏幕级前景回调由 ScreenManager.SetScreen 注入
    //Render 时控件之后调用不录 cache 每帧直接画 HUD 心动画每帧变化不走 retained mode cache
    private Action<IGuiRenderContext>? _renderForegroundHook;
    public Action<IGuiRenderContext>? RenderForegroundHook
    {
        get => _renderForegroundHook;
        set => _renderForegroundHook = value;
    }

    //WantsBlur 当前 Screen 是否需要 blur 后处理由 ScreenManager.SetScreen 从 Screen.WantsBlur 注入
    //blur 帧强制不走 retained mode cache BlurBeforeThisStratum 不录制到 cache 每帧须重新调
    public bool WantsBlur { get; set; }

    //FocusedControl 当前接收键盘输入的控件
    //ProcessMouseDown 命中控件后自动设为焦点 ProcessKeyDown 派发到此控件
    public GuiControl? FocusedControl
    {
        get => _focusedControl;
        set => _focusedControl = value;
    }

    public GuiWindow(int surfaceWidth, int surfaceHeight)
    {
        UpdateSurfaceSize(surfaceWidth, surfaceHeight);
    }

    //UpdateSurfaceSize 由 VulkanGuiApp 在创建和 swapchain 重建时调用
    //按实际像素计算 GuiScale 更新 Scaled 尺寸自身 Width/Height 用 scaled 供布局
    //MinScaled 320x240 对应原版 GUI 最小可读尺寸 guiScale 不会让控件小于此
    public void UpdateSurfaceSize(int actualW, int actualH)
    {
        const int MinScaledWidth = 320;
        const int MinScaledHeight = 240;
        var maxScaleW = actualW / MinScaledWidth;
        var maxScaleH = actualH / MinScaledHeight;
        var scale = Math.Max(1, Math.Min(maxScaleW, maxScaleH));
        GuiScale = scale;
        ScaledWidth = actualW / scale;
        ScaledHeight = actualH / scale;
        SurfaceWidth = actualW;
        SurfaceHeight = actualH;
        X = 0;
        Y = 0;
        Width = ScaledWidth;
        Height = ScaledHeight;
    }

    //ProcessMouseDown 处理鼠标按下事件自动派发给命中控件
    //Silk 回调给的是 actual 像素除 GuiScale 转 scaled 再 HitTest 与控件布局坐标一致
    //切换焦点时调旧控件 OnLostFocus 新控件 OnGotFocus 让 TextBox 启停光标闪烁
    public void ProcessMouseDown(GuiMouseButton button, int x, int y)
    {
        x /= GuiScale;
        y /= GuiScale;
        var hit = HitTest(x, y);
        if (hit is null) return;
        if (_focusedControl is not null && _focusedControl != hit)
            _focusedControl.OnLostFocus();
        var prevFocused = _focusedControl;
        _focusedControl = hit;
        if (prevFocused != hit) hit.OnGotFocus();
        var args = new MouseEventArgs(button, x, y, CurrentModifiers());
        if (hit == this)
        {
            base.OnMouseDown(args);
        }
        else
        {
            hit.OnMouseDown(args);
        }
    }

    //ProcessMouseUp 处理鼠标抬起
    //click 触发由控件自己决定 GuiButton 在 OnMouseUp 内判断按下位置一致后调 OnMouseClick
    public void ProcessMouseUp(GuiMouseButton button, int x, int y)
    {
        x /= GuiScale;
        y /= GuiScale;
        var hit = HitTest(x, y);
        if (hit is null) return;
        var args = new MouseEventArgs(button, x, y, CurrentModifiers());
        if (hit == this)
        {
            base.OnMouseUp(args);
        }
        else
        {
            hit.OnMouseUp(args);
        }
    }

    //ProcessMouseMove 处理鼠标移动并维护 hover 状态切换 enter/leave 事件
    public void ProcessMouseMove(int x, int y)
    {
        x /= GuiScale;
        y /= GuiScale;
        var args = new MouseEventArgs(GuiMouseButton.None, x, y, CurrentModifiers());
        var hit = HitTest(x, y);

        if (!ReferenceEquals(hit, _hoveredControl))
        {
            _hoveredControl?.OnMouseLeave(args);
            _hoveredControl = hit;
            hit?.OnMouseEnter(args);
        }

        if (hit is null)
        {
            base.OnMouseMove(args);
            return;
        }
        if (hit == this)
        {
            base.OnMouseMove(args);
        }
        else
        {
            hit.OnMouseMove(args);
        }
    }

    //GLFW_KEY_TAB 键码 Tab 键触发焦点导航不派发到焦点控件
    private const int TabKey = 258;

    //ProcessKeyDown 处理键盘按下并派发给焦点控件
    //Tab 键触发 FocusNext 焦点导航有字符输入时调 OnKeyPress 触发 TextBox 文本累积
    //先 UpdateModifiers 维护 Shift/Ctrl 状态构造 KeyEventArgs 传 Modifiers 供 TextBox 检测组合键
    public void ProcessKeyDown(int key, char ch = '\0')
    {
        UpdateModifiers(key, true);
        if (key == TabKey)
        {
            FocusNext();
            return;
        }
        var args = new KeyEventArgs(key, ch, CurrentModifiers());
        if (_focusedControl is not null && _focusedControl != this)
        {
            _focusedControl.OnKeyDown(args);
            if (ch != '\0')
            {
                _focusedControl.OnKeyPress(args);
            }
        }
        else
        {
            base.OnKeyDown(args);
            if (ch != '\0') base.OnKeyPress(args);
        }
    }

    //UpdateModifiers 根据 key 更新 _shiftDown/_ctrlDown 状态
    //Shift/Ctrl 按下设 true 抬起设 false Alt 暂未追踪 TextBox 不需要
    private void UpdateModifiers(int key, bool down)
    {
        if (key == GuiKeys.LeftShift || key == GuiKeys.RightShift) _shiftDown = down;
        else if (key == GuiKeys.LeftControl || key == GuiKeys.RightControl) _ctrlDown = down;
    }

    //CurrentModifiers 把 _shiftDown/_ctrlDown 组合成 KeyModifiers 标志位
    private KeyModifiers CurrentModifiers()
    {
        var m = KeyModifiers.None;
        if (_shiftDown) m |= KeyModifiers.Shift;
        if (_ctrlDown) m |= KeyModifiers.Control;
        return m;
    }

    //ProcessKeyUp 处理键盘抬起
    public void ProcessKeyUp(int key, char ch = '\0')
    {
        UpdateModifiers(key, false);
        var args = new KeyEventArgs(key, ch, CurrentModifiers());
        if (_focusedControl is not null && _focusedControl != this)
        {
            _focusedControl.OnKeyUp(args);
        }
        else
        {
            base.OnKeyUp(args);
        }
    }

    //ProcessKeyChar 处理字符输入只派发 OnKeyPress 到焦点控件用于 TextBox 文本累积
    //_ctrlDown 时短路 Ctrl 组合键不触发文本输入避免 Ctrl+V 粘贴同时插入 'v'
    public void ProcessKeyChar(char ch)
    {
        if (_ctrlDown) return;
        var args = new KeyEventArgs(0, ch, CurrentModifiers());
        if (_focusedControl is not null && _focusedControl != this)
            _focusedControl.OnKeyPress(args);
        else
            base.OnKeyPress(args);
    }

    //FocusNext 按 TabIndex 顺序切换焦点到下一个 TabStop 控件循环回到第一个
    //切换前调旧控件 OnLostFocus 新控件 OnGotFocus 让 TextBox 启停光标闪烁
    public void FocusNext()
    {
        var tabStops = new List<GuiControl>();
        CollectTabStops(this, tabStops);
        if (tabStops.Count == 0) return;
        tabStops.Sort((a, b) => a.TabIndex.CompareTo(b.TabIndex));
        var currentIdx = _focusedControl is null ? -1 : tabStops.IndexOf(_focusedControl);
        var nextIdx = (currentIdx + 1) % tabStops.Count;
        var next = tabStops[nextIdx];
        if (_focusedControl is not null && _focusedControl != next)
            _focusedControl.OnLostFocus();
        var prevFocused = _focusedControl;
        _focusedControl = next;
        if (prevFocused != next) next.OnGotFocus();
    }

    //CollectTabStops 深度遍历控件树收集 TabStop=true 且 Visible 且 Enabled 的控件
    private static void CollectTabStops(GuiContainer container, List<GuiControl> result)
    {
        foreach (var child in container.Children)
        {
            if (child is GuiContainer nested)
                CollectTabStops(nested, result);
            if (child.TabStop && child.Visible && child.Enabled)
                result.Add(child);
        }
    }

    public override void Render(IGuiRenderContext context)
    {
        //顶层窗口 cache 整窗口 dirty 时重 Render 录制 否则 ReplayRange 重放跳过 Render
        //WantsBlur 帧强制重 Render BlurBeforeThisStratum 不录制到 cache replay 会丢失 blur 标记
        if (!_isDirty && _renderCache is not null && !WantsBlur)
        {
            context.ReplayRange(_renderCache);
        }
        else
        {
            _renderCache ??= new();
            _renderCache.Clear();
            context.BeginRecording(_renderCache);
            //背景用 ScaledWidth/Height 逻辑像素 ToClip 转换以 scaled 为分母保证全屏覆盖
            context.DrawQuad(0, 0, ScaledWidth, ScaledHeight, BackgroundColor);
            //RenderBackgroundHook 画屏幕级背景纹理覆盖纯色背景
            RenderBackgroundHook?.Invoke(context);
            base.Render(context);
            context.EndRecording();
            ClearDirtyTree();
        }
        //RenderForegroundHook 控件之后每帧直接画不录 cache HUD 心动画每帧变化
        RenderForegroundHook?.Invoke(context);
    }
}
