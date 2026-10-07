namespace NetCraft.Gpu;

//CommonLayouts common layout factory, maps to vanilla net.minecraft.client.gui.layouts.CommonLayouts
//LabeledElement builds a vertical layout of label + element, label above and element below with spacing 4
public static class CommonLayouts
{
    private const int LabelSpacing = 4;

    //LabeledElement builds a vertical layout with a GuiLabel on top and the passed element below
    //maps to vanilla labeledElement omitting the Font param; on render GuiLabel gets it from the GlyphFont inside GuiRenderContext
    //label is a string rather than Component so the GPU layer does not depend on Network; rich text is handled by the domain layer
    //settings customizes the element cell's padding/align, maps to vanilla Consumer<LayoutSettings>
    public static ILayout LabeledElement(ILayoutElement element, string label, Action<LayoutSettings>? settings)
    {
        var layout = LinearLayout.Vertical().Spacing(LabelSpacing);
        layout.AddChild(new GuiLabel(label));
        var cellSettings = layout.NewCellSettings();
        settings?.Invoke(cellSettings);
        layout.AddChild(element, cellSettings);
        return layout;
    }

    //LabeledElement overload without settings, maps to vanilla labeledElement(font, element, label)
    public static ILayout LabeledElement(ILayoutElement element, string label)
        => LabeledElement(element, label, null);
}
