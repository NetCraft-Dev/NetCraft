namespace NetCraft.Gpu;

//CommonLayouts 常用布局工厂对标原版 net.minecraft.client.gui.layouts.CommonLayouts
//LabeledElement 构造标签+元素的垂直布局标签在上元素在下间距 4
public static class CommonLayouts
{
    private const int LabelSpacing = 4;

    //LabeledElement 构造垂直布局上方 GuiLabel 标签下方传入元素
    //对标原版 labeledElement 省略 Font 参数 GuiLabel 渲染时由 GuiRenderContext 内部 GlyphFont 提供
    //label 用 string 而非 Component 保持 Gpu 层不依赖 Network 富文本由业务层自行处理
    //settings 用于定制元素单元格的 padding/align 对标原版 Consumer<LayoutSettings>
    public static ILayout LabeledElement(ILayoutElement element, string label, Action<LayoutSettings>? settings)
    {
        var layout = LinearLayout.Vertical().Spacing(LabelSpacing);
        layout.AddChild(new GuiLabel(label));
        var cellSettings = layout.NewCellSettings();
        settings?.Invoke(cellSettings);
        layout.AddChild(element, cellSettings);
        return layout;
    }

    //LabeledElement 无 settings 重载对标原版 labeledElement(font, element, label)
    public static ILayout LabeledElement(ILayoutElement element, string label)
        => LabeledElement(element, label, null);
}
