using NetCraft.Game.Gui;
using NetCraft.Gpu;

namespace NetCraft.Game.Gui.Screens;

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
