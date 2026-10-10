using System.IO;
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

//PauseScreen pause menu, maps to vanilla PauseScreen
//Pops up on Esc during play, providing Resume/Options/Back to Main Menu buttons
//Background tiles the dirt texture, maps to vanilla options_background darken
public sealed class PauseScreen : Screen
{
    private GuiButton? _backToGameBtn;
    private GuiButton? _optionsBtn;
    private GuiButton? _quitToTitleBtn;
    //_dirtBg is not Added to Window; RenderBackground draws it directly to avoid duplication with the control tree
    private GuiImage? _dirtBg;

    public override string Title => "Game Menu";

    //WantsBlur the pause menu blurs the dirt background, maps to vanilla PauseScreen's blur effect
    //The BeforeBlur pass draws dirt; after blurring, the AfterBlur pass layers sharp buttons on top
    public override bool WantsBlur => true;

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
            Tint = GuiColor.FromRgb(64, 64, 64),
            X = 0,
            Y = 0,
            Width = GuiWidth,
            Height = GuiHeight
        };
        //Title centered above the buttons
        AddWidget(new GuiLabel("Game Menu") { X = cx - 100, Y = cy - 55, Width = 200, Height = 20 });
        var skin = RegisterButtonSprites();
        _backToGameBtn = AddWidget(new GuiButton("Back to Game") { X = cx - 100, Y = cy - 30, Width = 200, Height = 20 });
        ApplyButtonSpriteSkin(_backToGameBtn, skin);
        _backToGameBtn.Click += (_, _) => Manager.PopScreen();
        _optionsBtn = AddWidget(new GuiButton("Options") { X = cx - 100, Y = cy, Width = 200, Height = 20 });
        ApplyButtonSpriteSkin(_optionsBtn, skin);
        _optionsBtn.Click += (_, _) => Manager.PushScreen(new OptionsScreen());
        _quitToTitleBtn = AddWidget(new GuiButton("Save and Quit to Title") { X = cx - 100, Y = cy + 30, Width = 200, Height = 20 });
        ApplyButtonSpriteSkin(_quitToTitleBtn, skin);
        _quitToTitleBtn.Click += (_, _) => Manager.SetScreen(new TitleScreen());
    }

    //RenderBackground draws the tiled dirt background over the Window's solid color background
    //Before each redraw update Width/Height to match the ScaledWidth/Height after resize
    //At the end call BlurBeforeThisStratum so dirt goes into the BeforeBlur pass and subsequent controls into the AfterBlur pass
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
