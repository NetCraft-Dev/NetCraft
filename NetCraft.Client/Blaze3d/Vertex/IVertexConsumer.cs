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

namespace NetCraft.Client.Blaze3d.Vertex;

//IVertexConsumer vertex consumer interface, maps to vanilla VertexConsumer
//RenderState.BuildVertices writes vertex data through this interface
//Stage 4 StagedVertexBuffer implements this interface to stage vertices
public interface IVertexConsumer
{
    //AddVertexWith2DPose adds a vertex with a 2D pose transform: position+UV+color
    //pose is baked into the vertex position here; multiple elements in one Draw may use different poses without affecting batching
    void AddVertexWith2DPose(Matrix3x2 pose, float x, float y, float u, float v, int color);

    //AddVertex3D adds a 3D vertex position+color+uv+light+normal
    //position and normal are pre-transformed by the caller with PoseStack; the consumer only writes bytes per VertexFormat
    //color is an ARGB int and light is (block<<4)|(sky<<20) packed coords; both are written as float bit patterns
    //The default throws NotSupportedException; only the VertexBuilder of a 3D VertexFormat overrides it
    void AddVertex3D(float x, float y, float z, int color,
        float u, float v, int light, float nx, float ny, float nz)
        => throw new NotSupportedException("AddVertex3D is not implemented; this consumer does not support 3D vertices");
}
