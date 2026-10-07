using System.Text;

namespace NetCraft.Gpu;

//Button clickable button widget
//Clicking fires the Click event; supports pressed/hover/disabled visual states
//Nine-slice texture background with three states button.png/button_highlighted.png/button_disabled.png, corresponding to vanilla widget sprites
//P0 the three states use sprite identifiers; GuiSpriteManager dispatches nine-slice/tile/stretch by .mcmeta
//Falls back to a color block when BackgroundSprite is unset, for legacy code
public sealed class GuiButton : GuiControl
{
    private bool _pressed;
    private bool _hover;

    public string Text { get; set; } = string.Empty;
    public GuiColor BackgroundColor { get; set; } = GuiColor.FromRgb(60, 90, 160);
    public GuiColor HoverColor { get; set; } = GuiColor.FromRgb(80, 120, 200);
    public GuiColor PressedColor { get; set; } = GuiColor.FromRgb(40, 60, 120);
    public GuiColor ForegroundColor { get; set; } = GuiColor.White;

    //BackgroundSprite normal-state sprite identifier like minecraft:textures/gui/sprites/widget/button
    //null/an empty string means a color-block placeholder; GuiSpriteManager parses .mcmeta to decide nine_slice/stretch/tile
    public string BackgroundSprite { get; set; } = string.Empty;
    //HoverBackgroundSprite hover/pressed-state sprite identifier, falls back to BackgroundSprite when empty
    public string HoverBackgroundSprite { get; set; } = string.Empty;
    //DisabledBackgroundSprite disabled-state sprite identifier, falls back to BackgroundSprite when empty
    public string DisabledBackgroundSprite { get; set; } = string.Empty;
    //ImageTint texture tint color, white by default (no tint); the disabled state uses button_disabled.png's own gray so no extra tint is needed
    public GuiColor ImageTint { get; set; } = GuiColor.White;

    public event EventHandler<EventArgs>? Click;

    public GuiButton(string text = "")
    {
        Text = text;
    }

    protected internal override void OnMouseDown(MouseEventArgs e)
    {
        if (!Enabled) return;
        _pressed = true;
        base.OnMouseDown(e);
    }

    protected internal override void OnMouseUp(MouseEventArgs e)
    {
        if (!Enabled) return;
        var wasPressed = _pressed;
        _pressed = false;
        base.OnMouseUp(e);
        if (wasPressed && ContainsPoint(e.X, e.Y))
        {
            OnMouseClick(e);
        }
    }

    protected internal override void OnMouseClick(MouseEventArgs e)
    {
        if (!Enabled) return;
        base.OnMouseClick(e);
        Click?.Invoke(this, EventArgs.Empty);
    }

    protected internal override void OnMouseEnter(MouseEventArgs e)
    {
        _hover = true;
        base.OnMouseEnter(e);
    }

    protected internal override void OnMouseLeave(MouseEventArgs e)
    {
        _hover = false;
        _pressed = false;
        base.OnMouseLeave(e);
    }

    public override void Render(IGuiRenderContext context)
    {
        var sprite = BackgroundSprite;
        if (!Enabled)
        {
            if (!string.IsNullOrEmpty(DisabledBackgroundSprite)) sprite = DisabledBackgroundSprite;
        }
        else if (_pressed || _hover)
        {
            if (!string.IsNullOrEmpty(HoverBackgroundSprite)) sprite = HoverBackgroundSprite;
        }
        if (!string.IsNullOrEmpty(sprite))
        {
            //DrawSprite dispatched by GuiSpriteManager as nine_slice/stretch/tile by .mcmeta
            context.DrawSprite(sprite, X, Y, Width, Height, ImageTint);
        }
        else
        {
            var color = _pressed ? PressedColor : (_hover ? HoverColor : BackgroundColor);
            context.DrawQuad(X, Y, Width, Height, color);
        }
        if (!string.IsNullOrEmpty(Text))
        {
            var textX = X + Width / 2;
            var textY = Y + Height / 2;
            context.DrawText(textX, textY, Text, ForegroundColor);
        }
    }
}

//Label static text display widget
public sealed class GuiLabel : GuiControl
{
    public string Text { get; set; } = string.Empty;
    public GuiColor ForegroundColor { get; set; } = GuiColor.White;
    public GuiColor? BackgroundColor { get; set; }
    //TextAlign horizontal text alignment Left left Center center Right right
    public GuiTextAlign TextAlign { get; set; } = GuiTextAlign.Left;

    public GuiLabel(string text = "")
    {
        Text = text;
    }

    public override void Render(IGuiRenderContext context)
    {
        if (BackgroundColor is { } bg)
        {
            context.DrawQuad(X, Y, Width, Height, bg);
        }
        if (!string.IsNullOrEmpty(Text))
        {
            //Left alignment starts at X; Center/Right offset by MeasureText
            var textX = X;
            if (TextAlign != GuiTextAlign.Left)
            {
                var textWidth = context.MeasureText(Text);
                textX = TextAlign == GuiTextAlign.Center
                    ? X + (Width - (int)textWidth) / 2
                    : X + Width - (int)textWidth;
            }
            context.DrawText(textX, Y, Text, ForegroundColor);
        }
    }
}

//Panel container panel widget
//Can hold children and draw a background
public sealed class GuiPanel : GuiContainer
{
    public GuiColor BackgroundColor { get; set; } = GuiColor.FromRgb(50, 50, 50);

    public override void Render(IGuiRenderContext context)
    {
        context.DrawQuad(X, Y, Width, Height, BackgroundColor);
        base.Render(context);
    }
}

//TextBox text input widget
//Supports caret positioning/selection/keyboard navigation/copy-paste/caret blinking, aligned with vanilla EditBox behavior
//When focused, Update advances the blink timer and MarkDirty re-Renders every 0.5s to toggle caret visibility
//_charX caches character boundary X coordinates, updated on Render for ClickToCaretIndex to find the click position
//Clipboard static in-memory field shared across instances; not wired to the system clipboard yet
public sealed class GuiTextBox : GuiControl
{
    private readonly StringBuilder _text = new();
    private int _caretIndex;
    //_selectionAnchor selection anchor; -1 or equal to _caretIndex means no selection
    //Selection range min(anchor,caret)..max(anchor,caret)
    private int _selectionAnchor = -1;
    private bool _cursorVisible = true;
    private double _blinkAccumulator;
    private bool _isFocused;
    private bool _isDragging;
    //_charX[i] X coordinate before the i-th character; with caret=i the caret is drawn at _charX[i]
    private readonly List<int> _charX = new();
    //Clipboard in-memory clipboard shared across TextBox instances
    private static string _clipboard = string.Empty;

    private const int TextPaddingX = 4;
    private const int TextPaddingY = 4;
    private const double BlinkInterval = 0.5;

    public string Text
    {
        get => _text.ToString();
        set
        {
            _text.Clear();
            _text.Append(value);
            _caretIndex = _text.Length;
            _selectionAnchor = -1;
            MarkDirty();
        }
    }
    public GuiColor BackgroundColor { get; set; } = GuiColor.FromRgb(20, 20, 20);
    public GuiColor ForegroundColor { get; set; } = GuiColor.White;
    public GuiColor BorderColor { get; set; } = GuiColor.FromRgb(80, 80, 80);
    public GuiColor SelectionColor { get; set; } = GuiColor.FromRgba(0, 100, 200, 160);
    public GuiColor CursorColor { get; set; } = GuiColor.White;

    public event EventHandler<EventArgs>? TextChanged;

    private bool HasSelection => _selectionAnchor != -1 && _selectionAnchor != _caretIndex;
    private int SelectionStart => Math.Min(_selectionAnchor, _caretIndex);
    private int SelectionEnd => Math.Max(_selectionAnchor, _caretIndex);
    private string SelectedText =>
        HasSelection ? _text.ToString(SelectionStart, SelectionEnd - SelectionStart) : string.Empty;

    private void ClearSelection() => _selectionAnchor = -1;

    //DeleteSelection deletes the selection text, moves the caret to the selection start and clears the selection
    private void DeleteSelection()
    {
        if (!HasSelection) return;
        _text.Remove(SelectionStart, SelectionEnd - SelectionStart);
        _caretIndex = SelectionStart;
        ClearSelection();
        MarkDirty();
        TextChanged?.Invoke(this, EventArgs.Empty);
    }

    //InsertText inserts text at the caret; with a selection it deletes first, moves the caret and clears the selection
    private void InsertText(string text)
    {
        if (HasSelection) DeleteSelection();
        _text.Insert(_caretIndex, text);
        _caretIndex += text.Length;
        ClearSelection();
        MarkDirty();
        TextChanged?.Invoke(this, EventArgs.Empty);
    }

    protected internal override void OnGotFocus()
    {
        _isFocused = true;
        _cursorVisible = true;
        _blinkAccumulator = 0;
        MarkDirty();
    }

    protected internal override void OnLostFocus()
    {
        _isFocused = false;
        ClearSelection();
        MarkDirty();
    }

    //OnMouseDown positions the caret on click; Shift+click extends the selection, a plain click clears it, and dragging starts
    protected internal override void OnMouseDown(MouseEventArgs e)
    {
        _isDragging = true;
        var pos = ClickToCaretIndex(e.X);
        var shift = (e.Modifiers & KeyModifiers.Shift) != 0;
        if (shift && _isFocused)
        {
            if (_selectionAnchor == -1) _selectionAnchor = _caretIndex;
        }
        else
        {
            ClearSelection();
            _selectionAnchor = pos;
        }
        _caretIndex = pos;
        _cursorVisible = true;
        _blinkAccumulator = 0;
        MarkDirty();
        base.OnMouseDown(e);
    }

    //OnMouseMove while dragging the caret follows the mouse and the anchor stays fixed, forming a selection
    protected internal override void OnMouseMove(MouseEventArgs e)
    {
        if (!_isDragging) return;
        _caretIndex = ClickToCaretIndex(e.X);
        if (_selectionAnchor == -1) _selectionAnchor = _caretIndex;
        MarkDirty();
        base.OnMouseMove(e);
    }

    protected internal override void OnMouseUp(MouseEventArgs e)
    {
        _isDragging = false;
        base.OnMouseUp(e);
    }

    //ClickToCaretIndex finds the nearest character position from the mouse X using the _charX midpoint
    private int ClickToCaretIndex(int mouseX)
    {
        if (_charX.Count == 0) return _text.Length;
        if (mouseX <= _charX[0]) return 0;
        for (int i = 0; i < _charX.Count - 1; i++)
        {
            var mid = (_charX[i] + _charX[i + 1]) / 2;
            if (mouseX < mid) return i;
        }
        return _text.Length;
    }

    //OnKeyPress inserts printable characters; Ctrl combos short-circuit and BackSpace goes through OnKeyDown
    protected internal override void OnKeyPress(KeyEventArgs e)
    {
        if ((e.Modifiers & KeyModifiers.Control) != 0) return;
        if (e.Char == '\b') return;
        if (e.Char >= ' ' && e.Char != '\r' && e.Char != '\n')
        {
            InsertText(e.Char.ToString());
        }
        base.OnKeyPress(e);
    }

    //OnKeyDown handles editing keys Left/Right/Home/End/BackSpace/Delete/Ctrl+C/V/X/A
    protected internal override void OnKeyDown(KeyEventArgs e)
    {
        var ctrl = (e.Modifiers & KeyModifiers.Control) != 0;
        var shift = (e.Modifiers & KeyModifiers.Shift) != 0;
        switch (e.Key)
        {
            case GuiKeys.Left:
                MoveCaret(_caretIndex - 1, shift);
                break;
            case GuiKeys.Right:
                MoveCaret(_caretIndex + 1, shift);
                break;
            case GuiKeys.Home:
                MoveCaret(0, shift);
                break;
            case GuiKeys.End:
                MoveCaret(_text.Length, shift);
                break;
            case GuiKeys.BackSpace:
                if (HasSelection) DeleteSelection();
                else if (_caretIndex > 0)
                {
                    _text.Remove(_caretIndex - 1, 1);
                    _caretIndex--;
                    MarkDirty();
                    TextChanged?.Invoke(this, EventArgs.Empty);
                }
                break;
            case GuiKeys.Delete:
                if (HasSelection) DeleteSelection();
                else if (_caretIndex < _text.Length)
                {
                    _text.Remove(_caretIndex, 1);
                    MarkDirty();
                    TextChanged?.Invoke(this, EventArgs.Empty);
                }
                break;
            case GuiKeys.C:
                if (ctrl && HasSelection) _clipboard = SelectedText;
                break;
            case GuiKeys.X:
                if (ctrl && HasSelection)
                {
                    _clipboard = SelectedText;
                    DeleteSelection();
                }
                break;
            case GuiKeys.V:
                if (ctrl && _clipboard.Length > 0) InsertText(_clipboard);
                break;
            case GuiKeys.A:
                if (ctrl)
                {
                    _selectionAnchor = 0;
                    _caretIndex = _text.Length;
                    MarkDirty();
                }
                break;
        }
        base.OnKeyDown(e);
    }

    //MoveCaret moves the caret; shift extends the selection, otherwise it clears it, and resets caret visibility
    private void MoveCaret(int newIndex, bool shift)
    {
        newIndex = Math.Clamp(newIndex, 0, _text.Length);
        if (shift)
        {
            if (_selectionAnchor == -1) _selectionAnchor = _caretIndex;
        }
        else ClearSelection();
        _caretIndex = newIndex;
        _cursorVisible = true;
        _blinkAccumulator = 0;
        MarkDirty();
    }

    //Update advances the caret blink timer; when focused it toggles visibility every BlinkInterval and MarkDirty re-Renders
    public override void Update(double delta)
    {
        if (!_isFocused) return;
        _blinkAccumulator += delta;
        if (_blinkAccumulator >= BlinkInterval)
        {
            _blinkAccumulator -= BlinkInterval;
            _cursorVisible = !_cursorVisible;
            MarkDirty();
        }
    }

    public override void Render(IGuiRenderContext context)
    {
        context.DrawQuad(X, Y, Width, Height, BackgroundColor);
        context.DrawQuad(X, Y, Width, 1, BorderColor);
        context.DrawQuad(X, Y + Height - 1, Width, 1, BorderColor);
        context.DrawQuad(X, Y, 1, Height, BorderColor);
        context.DrawQuad(X + Width - 1, Y, 1, Height, BorderColor);
        var text = _text.ToString();
        var textX = X + TextPaddingX;
        var textY = Y + TextPaddingY;
        //Updates the _charX cache for ClickToCaretIndex, accumulating character widths via MeasureText
        _charX.Clear();
        _charX.Add(textX);
        for (int i = 0; i < text.Length; i++)
        {
            var w = context.MeasureText(text.Substring(0, i + 1));
            _charX.Add(textX + (int)w);
        }
        if (HasSelection)
        {
            var sx = _charX[SelectionStart];
            var ex = _charX[SelectionEnd];
            context.DrawQuad(sx, Y + 1, ex - sx, Height - 2, SelectionColor);
        }
        if (text.Length > 0)
        {
            context.DrawText(textX, textY, text, ForegroundColor);
        }
        if (_isFocused && _cursorVisible)
        {
            var idx = Math.Clamp(_caretIndex, 0, _charX.Count - 1);
            context.DrawQuad(_charX[idx], Y + 2, 1, Height - 4, CursorColor);
        }
    }
}

//Slider slider widget, corresponds to vanilla AbstractSliderButton
//Dragging updates Value, clamps to Min/Max, aligns by Step and fires ValueChanged
//The PoC does not support mouse capture; dragging stops outside the widget, so release inside it
public sealed class GuiSlider : GuiControl
{
    private bool _dragging;
    private bool _hover;

    public double Min { get; set; }
    public double Max { get; set; } = 100;
    public double Step { get; set; } = 1;

    private double _value;
    //The Value setter clamps to Min/Max and aligns by Step, firing ValueChanged only on change
    public double Value
    {
        get => _value;
        set
        {
            var aligned = AlignToStep(value);
            var clamped = Math.Clamp(aligned, Math.Min(Min, Max), Math.Max(Min, Max));
            if (Math.Abs(clamped - _value) < 1e-9) return;
            _value = clamped;
            ValueChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string Label { get; set; } = string.Empty;
    public GuiColor TrackColor { get; set; } = GuiColor.FromRgb(40, 40, 40);
    public GuiColor HandleColor { get; set; } = GuiColor.FromRgb(120, 120, 120);
    public GuiColor HandleActiveColor { get; set; } = GuiColor.FromRgb(180, 180, 200);
    public GuiColor ForegroundColor { get; set; } = GuiColor.White;

    public event EventHandler<EventArgs>? ValueChanged;

    public GuiSlider(double min = 0, double max = 100, double value = 0)
    {
        Min = min;
        Max = max;
        _value = Math.Clamp(AlignToStep(value), Math.Min(min, max), Math.Max(min, max));
    }

    //AlignToStep aligns any value to the nearest multiple of Step offset by Min
    private double AlignToStep(double raw)
    {
        if (Step <= 0) return raw;
        var steps = Math.Round((raw - Min) / Step);
        return Min + steps * Step;
    }

    //ValueFromX computes the Value for a mouse X coordinate
    private double ValueFromX(int x)
    {
        if (Width <= 0) return Min;
        var t = (double)(x - X) / Width;
        var raw = Min + t * (Max - Min);
        return raw;
    }

    //HandleX computes the handle's left-edge X; the handle is 8 wide and centered
    private int HandleX()
    {
        if (Math.Abs(Max - Min) < 1e-9) return X;
        var t = (Value - Min) / (Max - Min);
        return X + (int)(t * Width) - 4;
    }

    protected internal override void OnMouseDown(MouseEventArgs e)
    {
        if (!Enabled) return;
        _dragging = true;
        Value = ValueFromX(e.X);
        base.OnMouseDown(e);
    }

    protected internal override void OnMouseUp(MouseEventArgs e)
    {
        if (!Enabled) return;
        _dragging = false;
        base.OnMouseUp(e);
    }

    protected internal override void OnMouseMove(MouseEventArgs e)
    {
        if (!Enabled) return;
        if (_dragging) Value = ValueFromX(e.X);
        base.OnMouseMove(e);
    }

    protected internal override void OnMouseEnter(MouseEventArgs e)
    {
        _hover = true;
        base.OnMouseEnter(e);
    }

    protected internal override void OnMouseLeave(MouseEventArgs e)
    {
        _hover = false;
        _dragging = false;
        base.OnMouseLeave(e);
    }

    public override void Render(IGuiRenderContext context)
    {
        //Track centered, 4px tall
        var trackY = Y + Height / 2 - 2;
        context.DrawQuad(X, trackY, Width, 4, TrackColor);
        //Handle 8xHeight, centered
        var handleColor = _dragging ? HandleActiveColor : (_hover ? HandleActiveColor : HandleColor);
        context.DrawQuad(HandleX(), Y, 8, Height, handleColor);
        //Text at the top-left shows Label or Value
        var text = string.IsNullOrEmpty(Label) ? Value.ToString("0.##") : $"{Label}: {Value:0.##}";
        context.DrawText(X + 2, Y + 2, text, ForegroundColor);
    }
}

//Checkbox checkbox widget, corresponds to vanilla Checkbox
//Clicking toggles Checked and fires CheckedChanged; draws a box with a check fill and a text label
//The GuiControl base does not fire OnMouseClick automatically; check pressed inside OnMouseUp and call it
public sealed class GuiCheckbox : GuiControl
{
    private bool _hover;
    private bool _pressed;

    public bool Checked { get; set; }
    public string Text { get; set; } = string.Empty;
    public GuiColor BoxColor { get; set; } = GuiColor.FromRgb(60, 60, 60);
    public GuiColor HoverColor { get; set; } = GuiColor.FromRgb(90, 90, 90);
    public GuiColor CheckedColor { get; set; } = GuiColor.FromRgb(80, 200, 80);
    public GuiColor ForegroundColor { get; set; } = GuiColor.White;

    public event EventHandler<EventArgs>? CheckedChanged;

    public GuiCheckbox(string text = "", bool isChecked = false)
    {
        Text = text;
        Checked = isChecked;
    }

    protected internal override void OnMouseDown(MouseEventArgs e)
    {
        if (!Enabled) return;
        _pressed = true;
        base.OnMouseDown(e);
    }

    protected internal override void OnMouseUp(MouseEventArgs e)
    {
        if (!Enabled) return;
        var wasPressed = _pressed;
        _pressed = false;
        base.OnMouseUp(e);
        if (wasPressed && ContainsPoint(e.X, e.Y)) OnMouseClick(e);
    }

    protected internal override void OnMouseClick(MouseEventArgs e)
    {
        if (!Enabled) return;
        Checked = !Checked;
        CheckedChanged?.Invoke(this, EventArgs.Empty);
        base.OnMouseClick(e);
    }

    protected internal override void OnMouseEnter(MouseEventArgs e)
    {
        _hover = true;
        base.OnMouseEnter(e);
    }

    protected internal override void OnMouseLeave(MouseEventArgs e)
    {
        _hover = false;
        base.OnMouseLeave(e);
    }

    public override void Render(IGuiRenderContext context)
    {
        //The box size is taken from Height
        var boxSize = Height;
        var bg = _hover ? HoverColor : BoxColor;
        context.DrawQuad(X, Y, boxSize, boxSize, bg);
        //When checked, inset 2px and fill green
        if (Checked)
        {
            var pad = 2;
            context.DrawQuad(X + pad, Y + pad, boxSize - pad * 2, boxSize - pad * 2, CheckedColor);
        }
        if (!string.IsNullOrEmpty(Text))
        {
            context.DrawText(X + boxSize + 4, Y + 2, Text, ForegroundColor);
        }
    }
}

//Image image widget, corresponds to vanilla blit
//SpriteIdentifier when non-empty takes priority via DrawSprite, dispatched by GuiSpriteManager as nine_slice/stretch/tile by .mcmeta
//TextureId when non-zero calls DrawImage to sample a registered texture sub-region, otherwise uses a color-block + border placeholder
//TextureWidth/Height is the atlas total size; 0 means sample srcW/srcH across the whole image
public sealed class GuiImage : GuiControl
{
    public GuiColor BackgroundColor { get; set; } = GuiColor.FromRgb(80, 80, 80);
    public GuiColor? BorderColor { get; set; } = GuiColor.FromRgb(120, 120, 120);
    //SpriteIdentifier like minecraft:textures/gui/sprites/hud/crosshair; when non-empty, DrawSprite takes priority
    //.mcmeta is parsed by GuiSpriteManager to decide nine_slice/stretch/tile, maps to vanilla blitSprite
    public string SpriteIdentifier { get; set; } = string.Empty;
    //TextureId the texture id returned by RegisterTexture; 0 means no texture and uses the placeholder
    public int TextureId { get; set; }
    //TextureWidth/Height texture atlas total size used for UV computation; 0 samples the whole image
    public int TextureWidth { get; set; }
    public int TextureHeight { get; set; }
    //SourceU/V/W/H normalized UV coordinates 0~1 for the sampled sub-region
    public float SourceU { get; set; }
    public float SourceV { get; set; }
    public float SourceW { get; set; } = 1f;
    public float SourceH { get; set; } = 1f;
    //Tile=true tiles at the texture's original size to cover Width/Height, used for background textures like dirt
    //false stretches the whole texture to Width/Height
    public bool Tile { get; set; }
    //Tint texture color modulation; White leaves the original color; dirt backgrounds can layer a dark overlay
    public GuiColor Tint { get; set; } = GuiColor.White;

    public override void Render(IGuiRenderContext context)
    {
        //SpriteIdentifier takes priority via DrawSprite, dispatched by GuiSpriteManager by .mcmeta
        if (!string.IsNullOrEmpty(SpriteIdentifier))
        {
            context.DrawSprite(SpriteIdentifier, X, Y, Width, Height, Tint);
            return;
        }
        if (TextureId != 0)
        {
            if (Tile)
            {
                //Tile mode tiles on the texture's original-size grid, drawing the full texture per tile to cover Width/Height
                var ttw = TextureWidth > 0 ? TextureWidth : 16;
                var tth = TextureHeight > 0 ? TextureHeight : 16;
                var cols = (Width + ttw - 1) / ttw;
                var rows = (Height + tth - 1) / tth;
                for (int j = 0; j < rows; j++)
                    for (int i = 0; i < cols; i++)
                        context.DrawImage(TextureId, X + i * ttw, Y + j * tth, ttw, tth, 0, 0, ttw, tth, Tint);
                return;
            }
            //With a texture, normalized UVs are converted to pixel coordinates and the renderer derives the actual UVs
            var tw = TextureWidth > 0 ? TextureWidth : (int)(SourceW * 100);
            var th = TextureHeight > 0 ? TextureHeight : (int)(SourceH * 100);
            var srcX = (int)(SourceU * tw);
            var srcY = (int)(SourceV * th);
            var srcW = (int)(SourceW * tw);
            var srcH = (int)(SourceH * th);
            context.DrawImage(TextureId, X, Y, Width, Height, srcX, srcY, srcW, srcH, Tint);
            return;
        }
        //Without a texture, uses a color-block + border placeholder
        context.DrawQuad(X, Y, Width, Height, BackgroundColor);
        if (BorderColor is { } border)
        {
            //1px border on all four sides
            context.DrawQuad(X, Y, Width, 1, border);
            context.DrawQuad(X, Y + Height - 1, Width, 1, border);
            context.DrawQuad(X, Y, 1, Height, border);
            context.DrawQuad(X + Width - 1, Y, 1, Height, border);
        }
    }
}
