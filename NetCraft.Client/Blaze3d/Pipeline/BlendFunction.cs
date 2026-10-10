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
namespace NetCraft.Client.Blaze3d.Pipeline;

//BlendFunction blend function, maps to vanilla BlendFunction record
//Describes the blend equations for the color and alpha channels separately
//Provides static presets like LIGHTNING/TRANSLUCENT/INVERT for RenderPipelines to reference directly
public readonly record struct BlendFunction(BlendEquation Color, BlendEquation Alpha)
{
    //LIGHTNING lightning effect src_alpha + dst_alpha=1 additive
    public static readonly BlendFunction LIGHTNING = new(
        new BlendEquation(BlendFactor.SrcAlpha, BlendFactor.One, BlendOp.Add),
        new BlendEquation(BlendFactor.SrcAlpha, BlendFactor.One, BlendOp.Add));

    //GLINT glint effect src_color + dst_color=1, alpha not blended
    public static readonly BlendFunction GLINT = new(
        new BlendEquation(BlendFactor.SrcColor, BlendFactor.One, BlendOp.Add),
        new BlendEquation(BlendFactor.Zero, BlendFactor.One, BlendOp.Add));

    //OVERLAY overlay effect; color blends by src_alpha and alpha takes src
    public static readonly BlendFunction OVERLAY = new(
        new BlendEquation(BlendFactor.SrcAlpha, BlendFactor.One, BlendOp.Add),
        new BlendEquation(BlendFactor.One, BlendFactor.Zero, BlendOp.Add));

    //TRANSLUCENT translucent standard alpha blending; color and alpha both use src_alpha*(1-dst_alpha)
    public static readonly BlendFunction TRANSLUCENT = new(
        new BlendEquation(BlendFactor.SrcAlpha, BlendFactor.OneMinusSrcAlpha, BlendOp.Add),
        new BlendEquation(BlendFactor.One, BlendFactor.OneMinusSrcAlpha, BlendOp.Add));

    //TRANSLUCENT_PREMULTIPLIED_ALPHA premultiplied-alpha translucent; src is already premultiplied
    public static readonly BlendFunction TRANSLUCENT_PREMULTIPLIED_ALPHA = new(
        new BlendEquation(BlendFactor.One, BlendFactor.OneMinusSrcAlpha, BlendOp.Add),
        new BlendEquation(BlendFactor.One, BlendFactor.OneMinusSrcAlpha, BlendOp.Add));

    //ADDITIVE additive blending for glowing/highlight elements
    public static readonly BlendFunction ADDITIVE = new(
        new BlendEquation(BlendFactor.One, BlendFactor.One, BlendOp.Add),
        new BlendEquation(BlendFactor.One, BlendFactor.One, BlendOp.Add));

    //ENTITY_OUTLINE_BLIT entity outline blit; color blends by src_alpha and alpha takes 0
    public static readonly BlendFunction ENTITY_OUTLINE_BLIT = new(
        new BlendEquation(BlendFactor.SrcAlpha, BlendFactor.OneMinusSrcAlpha, BlendOp.Add),
        new BlendEquation(BlendFactor.Zero, BlendFactor.One, BlendOp.Add));

    //INVERT invert blending for inverted elements like the crosshair 1-dst_color / 1-src_color
    public static readonly BlendFunction INVERT = new(
        new BlendEquation(BlendFactor.OneMinusDstColor, BlendFactor.OneMinusSrcColor, BlendOp.Add),
        new BlendEquation(BlendFactor.One, BlendFactor.Zero, BlendOp.Add));

    //FromFactors convenience factory with shared src/dst factors + shared op
    public static BlendFunction FromFactors(BlendFactor src, BlendFactor dst, BlendOp op) =>
        new(new BlendEquation(src, dst, op), new BlendEquation(src, dst, op));

    //FromFactors independent factors for both channels, color and alpha sharing the ADD op
    public static BlendFunction FromFactors(
        BlendFactor srcColor, BlendFactor dstColor,
        BlendFactor srcAlpha, BlendFactor dstAlpha) =>
        new(
            new BlendEquation(srcColor, dstColor, BlendOp.Add),
            new BlendEquation(srcAlpha, dstAlpha, BlendOp.Add));
}
