using System.IO;
using NetCraft;
using NetCraft.Game.Client;
using NetCraft.Game.Gui.Screens;
using NetCraft.Gpu;

namespace NetCraft.Game.Gui;

//Screen screen base class, maps to vanilla net.minecraft.client.gui.screens.Screen
//Holds references to MinecraftClient and GuiWindow; subclasses create controls in Init and Add them to Window
//Lifecycle Init/Removed/OnClose/IsPauseScreen/Tick is driven by ScreenManager
public abstract class Screen
{
    //Minecraft client instance; access Config/Connection/SetScreen, etc.
    public MinecraftClient Minecraft { get; private set; } = null!;
    //Window render window; controls are Added directly to this Window
    public GuiWindow Window { get; private set; } = null!;
    //Manager screen manager reference, so OnClose can call PopScreen without going through MinecraftClient
    public ScreenManager Manager { get; private set; } = null!;
    //Title screen title, used for debugging and logging
    public virtual string Title => GetType().Name;
    //GuiWidth window render width; layout uses scaled logical pixels and control coordinates are based on it
    protected int GuiWidth => Window.ScaledWidth;
    //GuiHeight window render height; layout uses scaled logical pixels and control coordinates are based on it
    protected int GuiHeight => Window.ScaledHeight;

    //Attach injected by ScreenManager with MinecraftClient, Window, and the Manager itself
    internal void Attach(MinecraftClient minecraft, GuiWindow window, ScreenManager manager)
    {
        Minecraft = minecraft;
        Window = window;
        Manager = manager;
    }

    //Init called when the screen is entered, to create controls and layout
    public virtual void Init() { }

    //Removed called when the screen is left, to clean up resources
    public virtual void Removed() { }

    //OnClose called when the screen is closed, e.g. pressing Esc; by default PopScreen back to the previous level
    public virtual void OnClose()
    {
        Manager.PopScreen();
    }

    //IsPauseScreen whether it pauses game logic; main menu/pause menu return true
    public virtual bool IsPauseScreen() => false;

    //WantsBlur whether blur post-processing is needed; PauseScreen overrides it to return true
    //ScreenManager.SetScreen reads this value and injects GuiWindow.WantsBlur to trigger RenderBlurPasses
    public virtual bool WantsBlur => false;

    //Tick per-frame logic such as advancing animation state, driven by ScreenManager.Tick
    public virtual void Tick() { }

    //RenderBackground draws the screen-level background after the Window background quad and before controls
    //Empty by default so Window.BackgroundColor shows; subclasses override to draw dirt textures, etc.
    //Injected into GuiWindow.RenderBackgroundHook by ScreenManager.SetScreen
    public virtual void RenderBackground(IGuiRenderContext context) { }

    //RenderForeground draws the screen-level foreground after controls; not recorded in the retained mode cache, drawn directly every frame
    //Used for per-frame animated elements such as HUD hearts; injected into GuiWindow.RenderForegroundHook by ScreenManager.SetScreen
    public virtual void RenderForeground(IGuiRenderContext context) { }

    //OnF3Pressed called on F3 press; empty by default, GameScreen overrides it to toggle the Debug HUD
    public virtual void OnF3Pressed() { }

    //OnHotbarSelect called on number keys 1-9 with slot 0-8; GameScreen overrides it to switch the selected slot
    public virtual void OnHotbarSelect(int slot) { }

    //OnKeyPressed business keys not recognized by ScreenManager are passed down; subclasses decide using GameKeys (Q drop, E inventory, etc.)
    public virtual void OnKeyPressed(int key) { }

    //OnMouseDown raw mouse down with window coordinates; world interaction (block breaking) is overridden by GameScreen
    public virtual void OnMouseDown(GuiMouseButton button, int x, int y) { }

    //OnMouseUp raw mouse up with window coordinates
    public virtual void OnMouseUp(GuiMouseButton button, int x, int y) { }

    //AddWidget adds a control to Window and returns it for chaining
    protected T AddWidget<T>(T widget) where T : GuiControl
    {
        Window.Add(widget);
        return widget;
    }

    //CollectWidgets recursively collects GuiControls in an ILayoutElement tree
    //Layout containers recurse via VisitChildren; leaf GuiControls call collector to register into Window
    //When a Screen uses the Layout system, call this at the end of Init to register the layout's controls into Window
    protected static void CollectWidgets(ILayoutElement element, Action<GuiControl> collector)
    {
        if (element is GuiControl c) collector(c);
        else if (element is ILayout layout) layout.VisitChildren(child => CollectWidgets(child, collector));
    }

    //RegisterButtonSprites returns the default button's three-state sprite identifier
    //The identifier looks like minecraft:textures/gui/sprites/widget/button; GuiSpriteManager lazily loads the PNG+.mcmeta
    //The border is specified by .mcmeta's gui.scaling.border and not hardcoded in code: button.png border=3, button_disabled.png border=1
    //P0 replaces the old RegisterButtonTextures; no longer goes through RegisterTexture to get a textureId, loaded lazily inside GuiSpriteManager
    protected static (string Normal, string Hover, string Disabled) RegisterButtonSprites()
    {
        return (
            "minecraft:textures/gui/sprites/widget/button",
            "minecraft:textures/gui/sprites/widget/button_highlighted",
            "minecraft:textures/gui/sprites/widget/button_disabled");
    }

    //ApplyButtonSpriteSkin injects the identifier tuple from RegisterButtonSprites into GuiButton's three-state Sprite properties
    //The caller calls it once after AddWidget to finish sprite binding; at Render, DrawSprite dispatches by .mcmeta
    protected static void ApplyButtonSpriteSkin(GuiButton btn,
        (string Normal, string Hover, string Disabled) skin)
    {
        btn.BackgroundSprite = skin.Normal;
        btn.HoverBackgroundSprite = skin.Hover;
        btn.DisabledBackgroundSprite = skin.Disabled;
    }
}
