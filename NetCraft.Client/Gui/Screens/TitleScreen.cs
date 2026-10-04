using NetCraft;
using NetCraft.Game.Gui;
using NetCraft.Gpu;

namespace NetCraft.Game.Gui.Screens;

//TitleScreen 主菜单对应原版 TitleScreen
//启动首屏提供单人/多人/选项/退出按钮
//背景用 dirt 纹理平铺对应原版 options_background 平铺 dirt
public sealed class TitleScreen : Screen
{
    private GuiButton? _singlePlayerBtn;
    private GuiButton? _multiplayerBtn;
    private GuiButton? _optionsBtn;
    private GuiButton? _quitBtn;
    //_dirtBg 不 Add 到 Window 由 RenderBackground 直接绘制避免与控件树重复
    //每次 Init 重新调 RegisterTexture 取 textureId swapchain 重建后 id 变化也能更新
    private GuiImage? _dirtBg;

    public override string Title => "主菜单";

    public override void Init()
    {
        var cx = GuiWidth / 2;
        var cy = GuiHeight / 2;
        //dirt 背景平铺纹理加载失败 textureId=0 走灰色占位不阻塞菜单
        var dirtPath = Path.Combine(AppPaths.AssetsDir, "minecraft", "textures", "block", "dirt.png");
        var dirtId = Minecraft.GpuApp?.RegisterTexture(dirtPath) ?? 0;
        _dirtBg = new GuiImage
        {
            TextureId = dirtId,
            TextureWidth = 16,
            TextureHeight = 16,
            Tile = true,
            //原版 TitleScreen dirt 背景叠加深色 overlay 模拟 darken
            Tint = GuiColor.FromRgb(64, 64, 64),
            X = 0,
            Y = 0,
            Width = GuiWidth,
            Height = GuiHeight
        };
        //标题与副标题
        AddWidget(new GuiLabel("NetCraft") { X = cx - 60, Y = cy - 130, Width = 160, Height = 20 });
        AddWidget(new GuiLabel("v0.1.0 单人方块世界") { X = cx - 120, Y = cy - 105, Width = 240, Height = 16 });
        //主按钮列宽 240 高 25 间距 35
        var skin = RegisterButtonSprites();
        _singlePlayerBtn = AddWidget(new GuiButton("单人游戏") { X = cx - 120, Y = cy - 55, Width = 240, Height = 25 });
        ApplyButtonSpriteSkin(_singlePlayerBtn, skin);
        _singlePlayerBtn.Click += (_, _) => Manager.PushScreen(new GameScreen());
        _multiplayerBtn = AddWidget(new GuiButton("多人游戏") { X = cx - 120, Y = cy - 20, Width = 240, Height = 25 });
        ApplyButtonSpriteSkin(_multiplayerBtn, skin);
        //S3 连接本机 DedicatedServer 进入世界切 GameScreen 区块经 ChunkSender 渐进下发
        _multiplayerBtn.Click += (_, _) => Minecraft.ConnectServer("127.0.0.1", 25565);
        _optionsBtn = AddWidget(new GuiButton("选项") { X = cx - 120, Y = cy + 15, Width = 240, Height = 25 });
        ApplyButtonSpriteSkin(_optionsBtn, skin);
        _optionsBtn.Click += (_, _) => Manager.PushScreen(new OptionsScreen());
        _quitBtn = AddWidget(new GuiButton("退出游戏") { X = cx - 120, Y = cy + 50, Width = 240, Height = 25 });
        ApplyButtonSpriteSkin(_quitBtn, skin);
        _quitBtn.Click += (_, _) => Minecraft.Stop();
        //底部状态行
        AddWidget(new GuiLabel("© 2026 NetCraft | 按 F3 查看调试信息") { X = cx - 150, Y = GuiHeight - 18, Width = 300, Height = 14 });
    }

    //RenderBackground 画 dirt 平铺背景覆盖 Window 纯色背景
    //每次重画前更新 Width/Height 适配 resize 后的 ScaledWidth/Height
    public override void RenderBackground(IGuiRenderContext context)
    {
        if (_dirtBg is null) return;
        _dirtBg.Width = GuiWidth;
        _dirtBg.Height = GuiHeight;
        _dirtBg.Render(context);
    }

    public override void OnClose() => Minecraft.Stop();
    public override bool IsPauseScreen() => true;
}
