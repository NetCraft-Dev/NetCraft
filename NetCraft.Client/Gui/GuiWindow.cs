using NetCraft.Client.Blaze3d.Systems;
using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Textures;
using NetCraft.Client.Blaze3d.Shaders;
using NetCraft.Client.Blaze3d.Pipeline;
using NetCraft.Client.Blaze3d.Vertex;
using NetCraft.Client.Blaze3d.Vulkan;
using NetCraft.Client.Blaze3d.Platform;
using NetCraft.Client.Blaze3d.Font;
using NetCraft.Client.Blaze3d.Resource;
using NetCraft.Client.Blaze3d.Audio;
using NetCraft.Client.Blaze3d.Framegraph;
using NetCraft.Client.Blaze3d.Preprocessor;
using NetCraft.Client.Blaze3d.Util;
using NetCraft.Client.Render;
using NetCraft.Client.Render.Model;
using NetCraft.Client.Render.Texture;
using NetCraft.Client.Render.Texture.Atlas;
using NetCraft.Client.Render.Item;
using NetCraft.Client.Render.Entity;
using NetCraft.Client.Render.Entity.State;
using NetCraft.Client.Render.State.Gui;
using NetCraft.Client.Gui;
using NetCraft.Client.Gui.Render;
using NetCraft.Client.Gui.Render.State;
using NetCraft.Client.Gui.Render.Pip;
using NetCraft.Client.Gui.Navigation;
using NetCraft.Client.Gui.Layouts;
using NetCraft.Client.Gui.Font;
using NetCraft.Client.Gui.Font.Providers;
using NetCraft.Client.Gui.Font.Glyphs;
using NetCraft.Client.Model;
using NetCraft.Client.Model.Geom;
using NetCraft.Client.Resources.Metadata.Gui;
namespace NetCraft.Client.Gui;

//GuiWindow top-level GUI window
//Hosts the widget tree and exposes event input and the Render interface
//Does not manage Vulkan resources directly; an external caller passes in an IGuiRenderContext for rendering
public sealed class GuiWindow : GuiContainer
{
    private GuiControl? _hoveredControl;
    private GuiControl? _focusedControl;
    //_shiftDown/_ctrlDown maintained by ProcessKeyDown/Up from the Shift/Ctrl key codes
    //TextBox checks e.Modifiers to extend the selection or handle Ctrl+C/V/X combos
    private bool _shiftDown;
    private bool _ctrlDown;

    //SurfaceWidth/Height swapchain actual pixels, used for scissor/pipeline extent
    //ScaledWidth/Height logical pixels floor(Surface/GuiScale), used for widget layout
    //GuiScale integer factor max(1, min(SurfaceW/320, SurfaceH/240)) scaling widgets uniformly without blur
    public int SurfaceWidth { get; set; }
    public int SurfaceHeight { get; set; }
    public int GuiScale { get; private set; } = 1;
    public int ScaledWidth { get; private set; }
    public int ScaledHeight { get; private set; }

    private GuiColor _backgroundColor = GuiColor.FromRgb(30, 30, 30);
    //BackgroundColor background color; the setter MarkDirty's so a change re-Renders the whole window and records a new background quad
    public GuiColor BackgroundColor
    {
        get => _backgroundColor;
        set { _backgroundColor = value; MarkDirty(); }
    }

    //RenderBackgroundHook screen-level background callback injected by ScreenManager.SetScreen
    //Screen is in the Game layer and Window in the GPU layer; they cannot call each other directly, so this hook bridges them
    //Called on Render after the background quad and before widgets so a dirt background covers the solid color
    private Action<IGuiRenderContext>? _renderBackgroundHook;
    public Action<IGuiRenderContext>? RenderBackgroundHook
    {
        get => _renderBackgroundHook;
        set { _renderBackgroundHook = value; MarkDirty(); }
    }

    //RenderForegroundHook screen-level foreground callback injected by ScreenManager.SetScreen
    //Called on Render after widgets, not cached; HUD hearts animate every frame and skip the retained-mode cache
    private Action<IGuiRenderContext>? _renderForegroundHook;
    public Action<IGuiRenderContext>? RenderForegroundHook
    {
        get => _renderForegroundHook;
        set => _renderForegroundHook = value;
    }

    //WantsBlur whether the current Screen needs blur post-processing, injected by ScreenManager.SetScreen from Screen.WantsBlur
    //Blur frames skip the retained-mode cache; BlurBeforeThisStratum is not recorded and must be called every frame
    public bool WantsBlur { get; set; }

    //FocusedControl the widget currently receiving keyboard input
    //ProcessMouseDown sets the hit widget as focus; ProcessKeyDown dispatches to it
    public GuiControl? FocusedControl
    {
        get => _focusedControl;
        set => _focusedControl = value;
    }

    public GuiWindow(int surfaceWidth, int surfaceHeight)
    {
        UpdateSurfaceSize(surfaceWidth, surfaceHeight);
    }

    //UpdateSurfaceSize called by VulkanGuiApp on creation and swapchain recreation
    //Computes GuiScale from actual pixels and updates the Scaled sizes; its own Width/Height use scaled for layout
    //MinScaled 320x240 corresponds to the vanilla GUI minimum readable size; guiScale will not shrink widgets below this
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

    //ProcessMouseDown handles the mouse-down event and dispatches it to the hit widget automatically
    //The Silk callback gives actual pixels; divide by GuiScale to scaled, then HitTest to match widget layout coordinates
    //On focus change it calls OnLostFocus on the old widget and OnGotFocus on the new, letting TextBox start/stop caret blinking
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

    //ProcessMouseUp handles mouse-up
    //Click firing is up to the widget; GuiButton calls OnMouseClick in OnMouseUp after checking the press position matches
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

    //ProcessMouseMove handles mouse movement and maintains hover state, firing enter/leave events
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

    //GLFW_KEY_TAB key code; Tab triggers focus navigation and is not dispatched to the focused widget
    private const int TabKey = 258;

    //ProcessKeyDown handles key-down and dispatches to the focused widget
    //Tab triggers FocusNext focus navigation; with character input it calls OnKeyPress for TextBox text accumulation
    //First UpdateModifiers maintains the Shift/Ctrl state, builds KeyEventArgs and passes Modifiers for TextBox to detect combos
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

    //UpdateModifiers updates the _shiftDown/_ctrlDown state from key
    //Shift/Ctrl set true on press and false on release; Alt is not tracked as TextBox does not need it
    private void UpdateModifiers(int key, bool down)
    {
        if (key == GuiKeys.LeftShift || key == GuiKeys.RightShift) _shiftDown = down;
        else if (key == GuiKeys.LeftControl || key == GuiKeys.RightControl) _ctrlDown = down;
    }

    //CurrentModifiers combines _shiftDown/_ctrlDown into KeyModifiers flags
    private KeyModifiers CurrentModifiers()
    {
        var m = KeyModifiers.None;
        if (_shiftDown) m |= KeyModifiers.Shift;
        if (_ctrlDown) m |= KeyModifiers.Control;
        return m;
    }

    //ProcessKeyUp handles key-up
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

    //ProcessKeyChar handles character input, dispatching OnKeyPress only to the focused widget for TextBox accumulation
    //Short-circuits when _ctrlDown so Ctrl combos do not insert text, avoiding Ctrl+V pasting while also inserting 'v'
    public void ProcessKeyChar(char ch)
    {
        if (_ctrlDown) return;
        var args = new KeyEventArgs(0, ch, CurrentModifiers());
        if (_focusedControl is not null && _focusedControl != this)
            _focusedControl.OnKeyPress(args);
        else
            base.OnKeyPress(args);
    }

    //FocusNext moves focus to the next TabStop widget by TabIndex order, wrapping back to the first
    //Before switching it calls OnLostFocus on the old widget and OnGotFocus on the new, letting TextBox start/stop caret blinking
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

    //CollectTabStops depth-walks the widget tree collecting widgets with TabStop=true, Visible and Enabled
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
        //Top-level window cache: when the whole window is dirty it re-Renders and records, otherwise ReplayRange replays and skips Render
        //Blur frames force a re-Render; BlurBeforeThisStratum is not recorded, so replay would lose the blur marker
        if (!_isDirty && _renderCache is not null && !WantsBlur)
        {
            context.ReplayRange(_renderCache);
        }
        else
        {
            _renderCache ??= new();
            _renderCache.Clear();
            context.BeginRecording(_renderCache);
            //The background uses ScaledWidth/Height logical pixels; the ToClip conversion uses scaled as the denominator for full coverage
            context.DrawQuad(0, 0, ScaledWidth, ScaledHeight, BackgroundColor);
            //RenderBackgroundHook draws the screen-level background texture over the solid color
            RenderBackgroundHook?.Invoke(context);
            base.Render(context);
            context.EndRecording();
            ClearDirtyTree();
        }
        //RenderForegroundHook draws directly every frame after widgets without caching; HUD hearts change every frame
        RenderForegroundHook?.Invoke(context);
    }
}
