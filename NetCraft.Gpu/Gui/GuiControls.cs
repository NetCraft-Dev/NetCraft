using System.Text;

namespace NetCraft.Gpu;

//Button 可点击按钮控件
//点击触发 Click 事件支持 pressed/hover/disabled 视觉状态切换
//九宫格纹理背景三态 button.png/button_highlighted.png/button_disabled.png 对应原版 widget sprite
//P0 三态用 Sprite identifier 由 GuiSpriteManager 按 .mcmeta 分派九宫格/平铺/拉伸
//未设置 BackgroundSprite 时回退色块兼容旧代码
public sealed class GuiButton : GuiControl
{
    private bool _pressed;
    private bool _hover;

    public string Text { get; set; } = string.Empty;
    public GuiColor BackgroundColor { get; set; } = GuiColor.FromRgb(60, 90, 160);
    public GuiColor HoverColor { get; set; } = GuiColor.FromRgb(80, 120, 200);
    public GuiColor PressedColor { get; set; } = GuiColor.FromRgb(40, 60, 120);
    public GuiColor ForegroundColor { get; set; } = GuiColor.White;

    //BackgroundSprite 普通态 sprite identifier 形如 minecraft:textures/gui/sprites/widget/button
    //null/空字符串表示走色块占位 .mcmeta 由 GuiSpriteManager 解析决定 nine_slice/stretch/tile
    public string BackgroundSprite { get; set; } = string.Empty;
    //HoverBackgroundSprite 悬停/按下态 sprite identifier 空回退到 BackgroundSprite
    public string HoverBackgroundSprite { get; set; } = string.Empty;
    //DisabledBackgroundSprite 禁用态 sprite identifier 空回退到 BackgroundSprite
    public string DisabledBackgroundSprite { get; set; } = string.Empty;
    //ImageTint 纹理调制颜色默认白色不调制禁用态用 button_disabled.png 自身灰色不必额外 tint
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
            //DrawSprite 由 GuiSpriteManager 按 .mcmeta 分派 nine_slice/stretch/tile
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

//Label 静态文本显示控件
public sealed class GuiLabel : GuiControl
{
    public string Text { get; set; } = string.Empty;
    public GuiColor ForegroundColor { get; set; } = GuiColor.White;
    public GuiColor? BackgroundColor { get; set; }
    //TextAlign 文本水平对齐 Left 左对齐 Center 居中 Right 右对齐
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
            //Left 对齐直接从 X 开始 Center/Right 按 MeasureText 偏移
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

//Panel 容器面板控件
//可包含子控件并绘制背景
public sealed class GuiPanel : GuiContainer
{
    public GuiColor BackgroundColor { get; set; } = GuiColor.FromRgb(50, 50, 50);

    public override void Render(IGuiRenderContext context)
    {
        context.DrawQuad(X, Y, Width, Height, BackgroundColor);
        base.Render(context);
    }
}

//TextBox 文本输入控件
//支持光标定位/选区/键盘导航/复制粘贴/光标闪烁对齐原版 EditBox 行为
//聚焦时 Update 推进闪烁计时每 0.5s MarkDirty 重 Render 切换光标可见
//_charX 缓存字符边界 X 坐标 Render 时更新供 ClickToCaretIndex 查找点击位置
//Clipboard 静态内存字段跨实例共享暂不接入系统剪贴板
public sealed class GuiTextBox : GuiControl
{
    private readonly StringBuilder _text = new();
    private int _caretIndex;
    //_selectionAnchor 选区锚点 -1 或与 _caretIndex 相同表示无选区
    //选区范围 min(anchor,caret)..max(anchor,caret)
    private int _selectionAnchor = -1;
    private bool _cursorVisible = true;
    private double _blinkAccumulator;
    private bool _isFocused;
    private bool _isDragging;
    //_charX[i] 第 i 个字符前的 X 坐标 caret=i 时光标画在 _charX[i]
    private readonly List<int> _charX = new();
    //Clipboard 内存剪贴板跨 TextBox 实例共享
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

    //DeleteSelection 删选区文本 caret 移到选区起点清选区
    private void DeleteSelection()
    {
        if (!HasSelection) return;
        _text.Remove(SelectionStart, SelectionEnd - SelectionStart);
        _caretIndex = SelectionStart;
        ClearSelection();
        MarkDirty();
        TextChanged?.Invoke(this, EventArgs.Empty);
    }

    //InsertText 在 caret 处插入文本有选区先删 caret 前移清选区
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

    //OnMouseDown 点击定位光标 Shift+点击扩展选区 普通点击清选区 开始拖拽
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

    //OnMouseMove 拖拽中 caret 跟随鼠标 anchor 固定形成选区
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

    //ClickToCaretIndex 根据鼠标 X 找最近字符位置用 _charX 缓存中点判定
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

    //OnKeyPress 处理可打印字符插入 Ctrl 组合键短路 BackSpace 走 OnKeyDown
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

    //OnKeyDown 处理编辑键 Left/Right/Home/End/BackSpace/Delete/Ctrl+C/V/X/A
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

    //MoveCaret 移动光标 shift 扩展选区否则清选区 重置光标可见
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

    //Update 推进光标闪烁计时聚焦时每 BlinkInterval 切换可见 MarkDirty 重 Render
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
        //更新 _charX 缓存供 ClickToCaretIndex 用 MeasureText 累积字符宽度
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

//Slider 滑块控件对应原版 AbstractSliderButton
//拖动鼠标更新 Value 钳制到 Min/Max 并按 Step 对齐触发 ValueChanged
//PoC 不支持鼠标捕获拖出控件后停止拖动需在控件内释放
public sealed class GuiSlider : GuiControl
{
    private bool _dragging;
    private bool _hover;

    public double Min { get; set; }
    public double Max { get; set; } = 100;
    public double Step { get; set; } = 1;

    private double _value;
    //Value setter 钳制到 Min/Max 按 Step 对齐变化才触发 ValueChanged
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

    //AlignToStep 把任意值对齐到最近的 Step 整数倍偏移 Min
    private double AlignToStep(double raw)
    {
        if (Step <= 0) return raw;
        var steps = Math.Round((raw - Min) / Step);
        return Min + steps * Step;
    }

    //ValueFromX 根据鼠标 X 坐标计算对应 Value
    private double ValueFromX(int x)
    {
        if (Width <= 0) return Min;
        var t = (double)(x - X) / Width;
        var raw = Min + t * (Max - Min);
        return raw;
    }

    //HandleX 计算手柄左边缘 X 坐标手柄宽度 8 居中
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
        //轨道居中 4px 高
        var trackY = Y + Height / 2 - 2;
        context.DrawQuad(X, trackY, Width, 4, TrackColor);
        //手柄 8xHeight 居中
        var handleColor = _dragging ? HandleActiveColor : (_hover ? HandleActiveColor : HandleColor);
        context.DrawQuad(HandleX(), Y, 8, Height, handleColor);
        //文本左上角显示 Label 或 Value
        var text = string.IsNullOrEmpty(Label) ? Value.ToString("0.##") : $"{Label}: {Value:0.##}";
        context.DrawText(X + 2, Y + 2, text, ForegroundColor);
    }
}

//Checkbox 复选框控件对应原版 Checkbox
//点击切换 Checked 触发 CheckedChanged 渲染方框加勾选填充与文本标签
//基类 GuiControl 不自动触发 OnMouseClick 需在 OnMouseUp 内判断 pressed 后调
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
        //方框尺寸取 Height
        var boxSize = Height;
        var bg = _hover ? HoverColor : BoxColor;
        context.DrawQuad(X, Y, boxSize, boxSize, bg);
        //勾选时内缩 2px 填充绿色
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

//Image 图像控件对应原版 blit
//SpriteIdentifier 非空优先 DrawSprite 由 GuiSpriteManager 按 .mcmeta 分派 nine_slice/stretch/tile
//TextureId 非 0 调 DrawImage 采样已注册纹理子区域否则走色块+边框占位
//TextureWidth/Height 为图集总尺寸 0 表示按全图采样 srcW/srcH
public sealed class GuiImage : GuiControl
{
    public GuiColor BackgroundColor { get; set; } = GuiColor.FromRgb(80, 80, 80);
    public GuiColor? BorderColor { get; set; } = GuiColor.FromRgb(120, 120, 120);
    //SpriteIdentifier 形如 minecraft:textures/gui/sprites/hud/crosshair 非空优先走 DrawSprite
    //.mcmeta 由 GuiSpriteManager 解析决定 nine_slice/stretch/tile 对标原版 blitSprite
    public string SpriteIdentifier { get; set; } = string.Empty;
    //TextureId RegisterTexture 返回的纹理 id 0 表示无纹理走占位
    public int TextureId { get; set; }
    //TextureWidth/Height 纹理图集总尺寸 UV 计算用 0 时按全图采样
    public int TextureWidth { get; set; }
    public int TextureHeight { get; set; }
    //SourceU/V/W/H 归一化 UV 坐标 0~1 表示采样子区域
    public float SourceU { get; set; }
    public float SourceV { get; set; }
    public float SourceW { get; set; } = 1f;
    public float SourceH { get; set; } = 1f;
    //Tile=true 按纹理原始尺寸平铺覆盖 Width/Height 用于 dirt 等背景纹理
    //false 拉伸整个纹理到 Width/Height
    public bool Tile { get; set; }
    //Tint 纹理颜色调制 White 不调制原色 dirt 背景可叠加深色 overlay
    public GuiColor Tint { get; set; } = GuiColor.White;

    public override void Render(IGuiRenderContext context)
    {
        //SpriteIdentifier 优先走 DrawSprite 由 GuiSpriteManager 按 .mcmeta 分派
        if (!string.IsNullOrEmpty(SpriteIdentifier))
        {
            context.DrawSprite(SpriteIdentifier, X, Y, Width, Height, Tint);
            return;
        }
        if (TextureId != 0)
        {
            if (Tile)
            {
                //Tile 模式按纹理原始尺寸网格平铺每个 tile 画完整纹理覆盖 Width/Height
                var ttw = TextureWidth > 0 ? TextureWidth : 16;
                var tth = TextureHeight > 0 ? TextureHeight : 16;
                var cols = (Width + ttw - 1) / ttw;
                var rows = (Height + tth - 1) / tth;
                for (int j = 0; j < rows; j++)
                    for (int i = 0; i < cols; i++)
                        context.DrawImage(TextureId, X + i * ttw, Y + j * tth, ttw, tth, 0, 0, ttw, tth, Tint);
                return;
            }
            //有纹理时把归一化 UV 转像素坐标由 renderer 换算实际 UV
            var tw = TextureWidth > 0 ? TextureWidth : (int)(SourceW * 100);
            var th = TextureHeight > 0 ? TextureHeight : (int)(SourceH * 100);
            var srcX = (int)(SourceU * tw);
            var srcY = (int)(SourceV * th);
            var srcW = (int)(SourceW * tw);
            var srcH = (int)(SourceH * th);
            context.DrawImage(TextureId, X, Y, Width, Height, srcX, srcY, srcW, srcH, Tint);
            return;
        }
        //无纹理走色块+边框占位
        context.DrawQuad(X, Y, Width, Height, BackgroundColor);
        if (BorderColor is { } border)
        {
            //1px 边框四边
            context.DrawQuad(X, Y, Width, 1, border);
            context.DrawQuad(X, Y + Height - 1, Width, 1, border);
            context.DrawQuad(X, Y, 1, Height, border);
            context.DrawQuad(X + Width - 1, Y, 1, Height, border);
        }
    }
}
