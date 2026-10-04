using System.IO;
using NetCraft;
using NetCraft.Game.Gui;
using NetCraft.Gpu;

namespace NetCraft.Game.Gui.Screens;

//PauseScreen 暂停菜单对应原版 PauseScreen
//游戏中按 Esc 弹出提供继续/选项/回主菜单按钮
//背景用 dirt 纹理平铺对应原版 options_background 深色 darken
public sealed class PauseScreen : Screen
{
    private GuiButton? _backToGameBtn;
    private GuiButton? _optionsBtn;
    private GuiButton? _quitToTitleBtn;
    //_dirtBg 不 Add 到 Window 由 RenderBackground 直接绘制避免与控件树重复
    private GuiImage? _dirtBg;

    public override string Title => "游戏菜单";

    //WantsBlur 暂停菜单模糊 dirt 背景对应原版 PauseScreen 的 blur 效果
    //BeforeBlur 段画 dirt 模糊后 AfterBlur 段叠加清晰按钮
    public override bool WantsBlur => true;

    public override void Init()
    {
        var cx = GuiWidth / 2;
        var cy = GuiHeight / 2;
        //dirt 背景平铺加载失败 textureId=0 走灰色占位不阻塞菜单
        var dirtPath = Path.Combine(AppPaths.AssetsDir, "minecraft", "textures", "block", "dirt.png");
        var dirtId = Minecraft.GpuApp?.RegisterTexture(dirtPath) ?? 0;
        _dirtBg = new GuiImage
        {
            TextureId = dirtId,
            TextureWidth = 16,
            TextureHeight = 16,
            Tile = true,
            Tint = GuiColor.FromRgb(64, 64, 64),
            X = 0,
            Y = 0,
            Width = GuiWidth,
            Height = GuiHeight
        };
        //标题居中按钮上方
        AddWidget(new GuiLabel("游戏菜单") { X = cx - 100, Y = cy - 55, Width = 200, Height = 20 });
        var skin = RegisterButtonSprites();
        _backToGameBtn = AddWidget(new GuiButton("回到游戏") { X = cx - 100, Y = cy - 30, Width = 200, Height = 20 });
        ApplyButtonSpriteSkin(_backToGameBtn, skin);
        _backToGameBtn.Click += (_, _) => Manager.PopScreen();
        _optionsBtn = AddWidget(new GuiButton("选项") { X = cx - 100, Y = cy, Width = 200, Height = 20 });
        ApplyButtonSpriteSkin(_optionsBtn, skin);
        _optionsBtn.Click += (_, _) => Manager.PushScreen(new OptionsScreen());
        _quitToTitleBtn = AddWidget(new GuiButton("保存并退出到主菜单") { X = cx - 100, Y = cy + 30, Width = 200, Height = 20 });
        ApplyButtonSpriteSkin(_quitToTitleBtn, skin);
        _quitToTitleBtn.Click += (_, _) => Manager.SetScreen(new TitleScreen());
    }

    //RenderBackground 画 dirt 平铺背景覆盖 Window 纯色背景
    //每次重画前更新 Width/Height 适配 resize 后的 ScaledWidth/Height
    //末尾调 BlurBeforeThisStratum 让 dirt 进 BeforeBlur 段后续控件进 AfterBlur 段
    public override void RenderBackground(IGuiRenderContext context)
    {
        if (_dirtBg is null) return;
        _dirtBg.Width = GuiWidth;
        _dirtBg.Height = GuiHeight;
        _dirtBg.Render(context);
        context.BlurBeforeThisStratum();
    }

    public override void OnClose() => Manager.PopScreen();
    public override bool IsPauseScreen() => true;
}
