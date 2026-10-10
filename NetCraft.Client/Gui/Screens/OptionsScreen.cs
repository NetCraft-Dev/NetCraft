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

//OptionsScreen options menu, maps to vanilla OptionsScreen
//HeaderAndFooterLayout main layout, maps to vanilla: contents with three buttons stacked vertically, footer with Done
public sealed class OptionsScreen : Screen
{
    private HeaderAndFooterLayout? _layout;

    public override string Title => "Options";

    public override void Init()
    {
        var skin = RegisterButtonSprites();
        _layout = new HeaderAndFooterLayout(GuiWidth, GuiHeight);

        //contents three setting buttons stacked vertically; content is horizontally centered in contentsFrame
        var content = LinearLayout.Vertical().Spacing(10);
        content.AddChild(MakeNavButton("Video Settings", skin));
        content.AddChild(MakeNavButton("Sound Settings", skin));
        content.AddChild(MakeNavButton("Controls", skin));
        content.AddChild(MakeNavButton("Language", skin, () => Manager.PushScreen(new LanguageScreen())));
        _layout.AddToContents(content, LayoutSettings.Defaults().Align(0.5f, 0f));

        //footer Done button; footer is centered by default
        _layout.AddToFooter(MakeNavButton("Done", skin, OnClose));

        //Recursively collect GuiControls in the layout, register them into Window, then arrange and position
        _layout.VisitChildren(elem => CollectWidgets(elem, c => AddWidget(c)));
        _layout.ArrangeElements();
    }

    //MakeNavButton creates a navigation button with unified width/height + skin + optional click
    private GuiButton MakeNavButton(string text, (string Normal, string Hover, string Disabled) skin, System.Action? onClick = null)
    {
        var btn = new GuiButton(text) { Width = 200, Height = 20 };
        ApplyButtonSpriteSkin(btn, skin);
        if (onClick is not null) btn.Click += (_, _) => onClick();
        return btn;
    }
}
