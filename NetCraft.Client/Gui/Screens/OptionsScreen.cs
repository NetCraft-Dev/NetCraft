using NetCraft.Game.Gui;
using NetCraft.Gpu;

namespace NetCraft.Game.Gui.Screens;

//OptionsScreen 选项菜单对应原版 OptionsScreen
//HeaderAndFooterLayout 主布局对标原版 contents 三按钮垂直排列 footer 完成
public sealed class OptionsScreen : Screen
{
    private HeaderAndFooterLayout? _layout;

    public override string Title => "选项";

    public override void Init()
    {
        var skin = RegisterButtonSprites();
        _layout = new HeaderAndFooterLayout(GuiWidth, GuiHeight);

        //contents 三个设置按钮垂直排列 content 在 contentsFrame 水平居中
        var content = LinearLayout.Vertical().Spacing(10);
        content.AddChild(MakeNavButton("视频设置", skin));
        content.AddChild(MakeNavButton("音效设置", skin));
        content.AddChild(MakeNavButton("控制设置", skin));
        content.AddChild(MakeNavButton("语言", skin, () => Manager.PushScreen(new LanguageScreen())));
        _layout.AddToContents(content, LayoutSettings.Defaults().Align(0.5f, 0f));

        //footer 完成按钮 footer 默认居中
        _layout.AddToFooter(MakeNavButton("完成", skin, OnClose));

        //递归收集 layout 内 GuiControl 注册到 Window 后排列定位
        _layout.VisitChildren(elem => CollectWidgets(elem, c => AddWidget(c)));
        _layout.ArrangeElements();
    }

    //MakeNavButton 创建导航按钮统一宽高+皮肤+可选点击
    private GuiButton MakeNavButton(string text, (string Normal, string Hover, string Disabled) skin, System.Action? onClick = null)
    {
        var btn = new GuiButton(text) { Width = 200, Height = 20 };
        ApplyButtonSpriteSkin(btn, skin);
        if (onClick is not null) btn.Click += (_, _) => onClick();
        return btn;
    }
}
