using NetCraft.Game.Client;
using NetCraft.Gpu;
using NetCraft.Logging;

namespace NetCraft.Game.Gui;

//ScreenManager 屏幕管理器对应原版 Minecraft.screen 字段
//持当前 Screen 并驱动生命周期切换时清理旧控件调旧 Removed 新 Init
//历史栈支持 PushScreen/PopScreen 回上一级 Esc 键触发当前 Screen.OnClose
public sealed class ScreenManager
{
    private readonly MinecraftClient _minecraft;
    private readonly GuiWindow _window;
    private Screen? _current;
    //_history 屏幕历史栈 PushScreen 压栈 PopScreen 弹出回上一级
    private readonly Stack<Screen> _history = new();
    //_layoutDirty 窗口 resize 标志由 SwapchainRecreated 设置 ProcessLayoutIfDirty 在 Tick 线程消费
    //阶段7 控件树归 Tick 线程 resize 回调不能直接调 Window.Clear/Screen.Init 必延迟到 Tick
    private volatile bool _layoutDirty;

    public Screen? Current => _current;

    public ScreenManager(MinecraftClient minecraft, GuiWindow window)
    {
        _minecraft = minecraft;
        _window = window;
    }

    //SetScreen 切换屏幕先 Removed 旧的再清理控件再 Init 新的不入历史栈
    //同时注入 RenderBackgroundHook 让 Screen.RenderBackground 在 Window 背景层绘制
    public void SetScreen(Screen? screen)
    {
        var old = _current;
        if (old is not null)
        {
            old.Removed();
            Log.Debug($"SetScreen exit old={old.Title}");
        }
        _window.Clear();
        _current = screen;
        //screen 为 null 时清空背景/前景 hook 避免旧 Screen 的渲染残留
        _window.RenderBackgroundHook = screen is null ? null : ctx => screen.RenderBackground(ctx);
        _window.RenderForegroundHook = screen is null ? null : ctx => screen.RenderForeground(ctx);
        //WantsBlur 从 Screen 注入 GuiWindow 触发 RenderBlurPasses 分段渲染
        _window.WantsBlur = screen?.WantsBlur ?? false;
        if (screen is null) return;
        Log.Debug($"SetScreen entry new={screen.Title}");
        screen.Attach(_minecraft, _window, this);
        screen.Init();
    }

    //PushScreen 压栈当前屏幕并切换到新屏幕用于进入子菜单
    public void PushScreen(Screen screen)
    {
        if (_current is not null) _history.Push(_current);
        SetScreen(screen);
    }

    //PopScreen 弹出历史栈回上一级栈空回 null 由 Esc 或 OnClose 触发
    public void PopScreen()
    {
        if (_history.Count > 0) SetScreen(_history.Pop());
        else SetScreen(null);
    }

    //HandleRawKeyDown 处理原始键码识别业务键后调当前屏幕对应回调
    //Esc 切屏 F3 切 Debug 数字键选 Hotbar 槽位 其余业务键(Q 丢弃 E 背包)原样下发不消费
    public void HandleRawKeyDown(int key)
    {
        if (_current is null) return;
        if (key == GameKeys.Escape)
            _current.OnClose();
        else if (key == GameKeys.F3)
            _current.OnF3Pressed();
        else if (key >= GameKeys.D1 && key <= GameKeys.D9)
            _current.OnHotbarSelect(key - GameKeys.D1);
        else if (key >= GameKeys.Keypad1 && key <= GameKeys.Keypad9)
            _current.OnHotbarSelect(key - GameKeys.Keypad1);
        else
            _current.OnKeyPressed(key);
    }

    //HandleRawMouseDown 原始鼠标按下转发给当前屏幕 世界交互(挖方块)走这条路
    public void HandleRawMouseDown(GuiMouseButton button, int x, int y)
        => _current?.OnMouseDown(button, x, y);

    //HandleRawMouseUp 原始鼠标抬起转发给当前屏幕
    public void HandleRawMouseUp(GuiMouseButton button, int x, int y)
        => _current?.OnMouseUp(button, x, y);

    //MouseX/MouseY 最近一次鼠标位置 已按 GuiScale 换算成控件坐标系
    //背包界面按它判悬停槽 Q 丢弃作用于悬停槽
    public int MouseX { get; private set; }
    public int MouseY { get; private set; }

    //HandleRawMouseMove 记录鼠标位置 窗口坐标换算成控件坐标
    public void HandleRawMouseMove(int x, int y)
    {
        var scale = Math.Max(1, _window.GuiScale);
        MouseX = x / scale;
        MouseY = y / scale;
    }

    public void Tick()
    {
        _current?.Tick();
    }

    //Resized 窗口尺寸变化时由 MinecraftClient 订阅 SwapchainRecreated 触发
    //阶段7 控件树归 Tick 线程 resize 回调在 Render 线程触发不能直接调 Window.Clear/Screen.Init
    //只设 _layoutDirty 标志 ProcessLayoutIfDirty 由 MinecraftClient.OnFrameTick 在 Tick 线程消费
    public void Resized()
    {
        _layoutDirty = true;
    }

    //ProcessLayoutIfDirty Tick 线程每帧调检测 _layoutDirty 真则清控件重 Init 当前 Screen
    //由 MinecraftClient.OnFrameTick 在 PollInput 后 Window.Update 前调用独占控件树无竞争
    public void ProcessLayoutIfDirty()
    {
        if (!_layoutDirty) return;
        _layoutDirty = false;
        if (_current is null) return;
        _window.Clear();
        _current.Init();
    }
}
