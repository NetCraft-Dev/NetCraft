using NetCraft;
using NetCraft.Client.Gui;
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

namespace NetCraft.Client.Gui.Screens;

//TitleScreen main menu, maps to vanilla TitleScreen
//The first screen on startup, providing Singleplayer/Multiplayer/Options/Quit buttons
//Background tiles the dirt texture, maps to vanilla options_background tiling dirt
public sealed class TitleScreen : Screen
{
    private GuiButton? _singlePlayerBtn;
    private GuiButton? _multiplayerBtn;
    private GuiButton? _optionsBtn;
    private GuiButton? _quitBtn;
    //_dirtBg is not Added to Window; RenderBackground draws it directly to avoid duplication with the control tree
    //Each Init calls RegisterTexture again to get a textureId, so it updates when the id changes after a swapchain rebuild
    private GuiImage? _dirtBg;

    public override string Title => "Main Menu";

    public override void Init()
    {
        var cx = GuiWidth / 2;
        var cy = GuiHeight / 2;
        //If the dirt background tile texture fails to load, textureId=0 falls back to a gray placeholder without blocking the menu
        var dirtPath = Path.Combine(AppPaths.AssetsDir, "minecraft", "textures", "block", "dirt.png");
        var dirtId = Minecraft.GpuApp?.RegisterTexture(dirtPath) ?? 0;
        _dirtBg = new GuiImage
        {
            TextureId = dirtId,
            TextureWidth = 16,
            TextureHeight = 16,
            Tile = true,
            //Vanilla TitleScreen overlays a dark tint on the dirt background to simulate darken
            Tint = GuiColor.FromRgb(64, 64, 64),
            X = 0,
            Y = 0,
            Width = GuiWidth,
            Height = GuiHeight
        };
        //Title and subtitle
        AddWidget(new GuiLabel("NetCraft") { X = cx - 60, Y = cy - 130, Width = 160, Height = 20 });
        AddWidget(new GuiLabel("v0.1.0 Singleplayer Block World") { X = cx - 120, Y = cy - 105, Width = 240, Height = 16 });
        //Main button column: width 240, height 25, spacing 35
        var skin = RegisterButtonSprites();
        _singlePlayerBtn = AddWidget(new GuiButton("Singleplayer") { X = cx - 120, Y = cy - 55, Width = 240, Height = 25 });
        ApplyButtonSpriteSkin(_singlePlayerBtn, skin);
        _singlePlayerBtn.Click += (_, _) => Manager.PushScreen(new GameScreen());
        _multiplayerBtn = AddWidget(new GuiButton("Multiplayer") { X = cx - 120, Y = cy - 20, Width = 240, Height = 25 });
        ApplyButtonSpriteSkin(_multiplayerBtn, skin);
        //S3 connects to the local DedicatedServer, enters the world, switches to GameScreen; chunks are sent progressively via ChunkSender
        _multiplayerBtn.Click += (_, _) => Minecraft.ConnectServer("127.0.0.1", 25565);
        _optionsBtn = AddWidget(new GuiButton("Options") { X = cx - 120, Y = cy + 15, Width = 240, Height = 25 });
        ApplyButtonSpriteSkin(_optionsBtn, skin);
        _optionsBtn.Click += (_, _) => Manager.PushScreen(new OptionsScreen());
        _quitBtn = AddWidget(new GuiButton("Quit Game") { X = cx - 120, Y = cy + 50, Width = 240, Height = 25 });
        ApplyButtonSpriteSkin(_quitBtn, skin);
        _quitBtn.Click += (_, _) => Minecraft.Stop();
        //Bottom status line
        AddWidget(new GuiLabel("© 2026 NetCraft | Press F3 for debug info") { X = cx - 150, Y = GuiHeight - 18, Width = 300, Height = 14 });
    }

    //RenderBackground draws the tiled dirt background over the Window's solid color background
    //Before each redraw update Width/Height to match the ScaledWidth/Height after resize
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
