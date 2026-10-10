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
using NetCraft.Client.Gui;
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
namespace NetCraft.Client.Gui.Layouts;

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
