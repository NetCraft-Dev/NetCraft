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

//DepthStencilState depth-stencil state, maps to vanilla DepthStencilState record
//depthTest=null means depth testing is disabled; writeDepth controls whether the depth buffer is written
//depthBiasScaleFactor/depthBiasConstant used to eliminate z-fighting
public readonly record struct DepthStencilState(
    CompareOp? DepthTest,
    bool WriteDepth,
    float DepthBiasScaleFactor,
    float DepthBiasConstant)
{
    //DEFAULT default depth state Less write no bias
    //The current Camera projection uses the standard Vulkan [0,1] depth near=0 far=1; Less matches clearDepth=1
    //Vanilla uses GreaterOrEqual because of reversed-Z; NetCraft does not use reversed-Z and switches to Less for consistency
    public static readonly DepthStencilState DEFAULT = new(CompareOp.Less, true, 0f, 0f);

    public DepthStencilState(CompareOp depthTest, bool depthWrite)
        : this(depthTest, depthWrite, 0f, 0f) { }
}
