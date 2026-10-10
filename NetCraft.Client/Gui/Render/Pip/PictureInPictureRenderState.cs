using System.Numerics;
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

namespace NetCraft.Client.Gui.Render.Pip;

//PictureInPictureRenderState PIP render state interface, maps to vanilla pip.PictureInPictureRenderState
//Render state value object for 3D content embedded in the GUI (player skins/banners/entity previews)
//Built and submitted by Screen during the submission phase into GuiRenderState.AddPictureInPicture
//During the render phase the PictureInPictureRenderer<T> subclass renders 3D content offscreen then blits it to the GUI
public interface PictureInPictureRenderState
{
    //X0/Y0/X1/Y1 the PIP area rectangle in GUI coordinates, top-left/bottom-right, maps to vanilla x0/y0/x1/y1
    int X0 { get; }
    int Y0 { get; }
    int X1 { get; }
    int Y1 { get; }

    //Scale 3D content scale factor, maps to vanilla scale, set by the subclass per model size
    float Scale { get; }

    //ScissorArea scissor rectangle; empty means no clipping
    ScreenRectangle ScissorArea { get; }

    //Pose 2D transform matrix, identity by default, maps to vanilla pose() default IDENTITY_POSE
    //The PIP area usually has no pose transform; blit uses X0/Y0/X1/Y1 directly
    Matrix3x2 Pose { get; }

    //Bounds used for level intersection tests, derived from intersecting the geometry rectangle with the scissor
    ScreenRectangle Bounds { get; }

    //GetBounds computes the bounds by intersecting the geometry rectangle with the scissor, maps to vanilla getBounds
    //Uses the geometry rectangle directly when the scissor is empty, otherwise the intersection
    public static ScreenRectangle GetBounds(int x0, int y0, int x1, int y1, ScreenRectangle scissorArea)
    {
        var raw = new ScreenRectangle(x0, y0, x1 - x0, y1 - y0);
        return scissorArea.IsEmpty ? raw : scissorArea.Intersect(raw) ?? raw;
    }
}
