using System.IO;
using NetCraft;
using NetCraft.Game.Client;
using NetCraft.Game.Gui.Screens;
using NetCraft.Gpu;

namespace NetCraft.Game.Gui;

//Screen 屏幕基类对应原版 net.minecraft.client.gui.screens.Screen
//持 MinecraftClient 与 GuiWindow 引用子类在 Init 创建控件 Add 到 Window
//生命周期 Init/Removed/OnClose/IsPauseScreen/Tick 由 ScreenManager 驱动
public abstract class Screen
{
    //Minecraft 客户端实例访问 Config/Connection/SetScreen 等
    public MinecraftClient Minecraft { get; private set; } = null!;
    //Window 渲染窗口控件直接 Add 到此 Window
    public GuiWindow Window { get; private set; } = null!;
    //Manager 屏幕管理器引用用于 OnClose 调 PopScreen 不绕道 MinecraftClient
    public ScreenManager Manager { get; private set; } = null!;
    //Title 屏幕标题用于调试与日志
    public virtual string Title => GetType().Name;
    //GuiWidth 窗口渲染宽度布局用 scaled 逻辑像素控件坐标以此为基准
    protected int GuiWidth => Window.ScaledWidth;
    //GuiHeight 窗口渲染高度布局用 scaled 逻辑像素控件坐标以此为基准
    protected int GuiHeight => Window.ScaledHeight;

    //Attach 由 ScreenManager 注入 MinecraftClient Window 与 Manager 自身
    internal void Attach(MinecraftClient minecraft, GuiWindow window, ScreenManager manager)
    {
        Minecraft = minecraft;
        Window = window;
        Manager = manager;
    }

    //Init 屏幕被切入时调用于创建控件布局
    public virtual void Init() { }

    //Removed 屏幕被切走时调用于清理资源
    public virtual void Removed() { }

    //OnClose 屏幕被关闭时调如按 Esc 默认 PopScreen 回上一级
    public virtual void OnClose()
    {
        Manager.PopScreen();
    }

    //IsPauseScreen 是否暂停游戏逻辑主菜单/暂停菜单返回 true
    public virtual bool IsPauseScreen() => false;

    //WantsBlur 是否需要 blur 后处理 PauseScreen 重写返回 true
    //ScreenManager.SetScreen 读取此值注入 GuiWindow.WantsBlur 触发 RenderBlurPasses
    public virtual bool WantsBlur => false;

    //Tick 每帧逻辑推进动画状态等由 ScreenManager.Tick 驱动
    public virtual void Tick() { }

    //RenderBackground 在 Window 背景 quad 之后控件之前绘制屏幕级背景
    //默认空让 Window.BackgroundColor 显示子类重写画 dirt 纹理等
    //由 ScreenManager.SetScreen 注入到 GuiWindow.RenderBackgroundHook
    public virtual void RenderBackground(IGuiRenderContext context) { }

    //RenderForeground 在控件之后绘制屏幕级前景不录 retained mode cache 每帧直接画
    //用于 HUD 心等每帧动画元素由 ScreenManager.SetScreen 注入到 GuiWindow.RenderForegroundHook
    public virtual void RenderForeground(IGuiRenderContext context) { }

    //OnF3Pressed 按下 F3 时调默认空 GameScreen 重写切换 Debug HUD 显示
    public virtual void OnF3Pressed() { }

    //OnHotbarSelect 按数字键 1-9 时调 slot 0-8 GameScreen 重写切换选中槽位
    public virtual void OnHotbarSelect(int slot) { }

    //OnKeyPressed 未被 ScreenManager 识别的业务键下发 子类按 GameKeys 自行判断(Q 丢弃 E 背包等)
    public virtual void OnKeyPressed(int key) { }

    //OnMouseDown 原始鼠标按下带窗口坐标 世界交互(挖方块)由 GameScreen 重写
    public virtual void OnMouseDown(GuiMouseButton button, int x, int y) { }

    //OnMouseUp 原始鼠标抬起带窗口坐标
    public virtual void OnMouseUp(GuiMouseButton button, int x, int y) { }

    //AddWidget 把控件加到 Window 返回该控件用于链式调用
    protected T AddWidget<T>(T widget) where T : GuiControl
    {
        Window.Add(widget);
        return widget;
    }

    //CollectWidgets 递归收集 ILayoutElement 树内的 GuiControl
    //Layout 容器递归 VisitChildren 叶子 GuiControl 调 collector 注册到 Window
    //Screen 用 Layout 体系时 Init 末尾调此方法把布局内控件注册到 Window
    protected static void CollectWidgets(ILayoutElement element, Action<GuiControl> collector)
    {
        if (element is GuiControl c) collector(c);
        else if (element is ILayout layout) layout.VisitChildren(child => CollectWidgets(child, collector));
    }

    //RegisterButtonSprites 返回默认按钮三态 sprite identifier
    //identifier 形如 minecraft:textures/gui/sprites/widget/button 由 GuiSpriteManager 懒加载 PNG+.mcmeta
    //border 由 .mcmeta 的 gui.scaling.border 指定不在代码里硬编码 button.png border=3 button_disabled.png border=1
    //P0 替代旧 RegisterButtonTextures 不再走 RegisterTexture 取 textureId 由 GuiSpriteManager 内部懒加载
    protected static (string Normal, string Hover, string Disabled) RegisterButtonSprites()
    {
        return (
            "minecraft:textures/gui/sprites/widget/button",
            "minecraft:textures/gui/sprites/widget/button_highlighted",
            "minecraft:textures/gui/sprites/widget/button_disabled");
    }

    //ApplyButtonSpriteSkin 把 RegisterButtonSprites 返回的 identifier 元组注入 GuiButton 三态 Sprite 属性
    //调用方在 AddWidget 后调一次完成 sprite 绑定 Render 时由 DrawSprite 按 .mcmeta 分派
    protected static void ApplyButtonSpriteSkin(GuiButton btn,
        (string Normal, string Hover, string Disabled) skin)
    {
        btn.BackgroundSprite = skin.Normal;
        btn.HoverBackgroundSprite = skin.Hover;
        btn.DisabledBackgroundSprite = skin.Disabled;
    }
}
