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

namespace NetCraft.Client.Render.Item;

//VertexConsumer3D 3D vertex consumer, maps to vanilla VertexConsumer
//Implements IVertexConsumer, writing 3D vertices into List<float> for the P14 chain ItemPipRenderer/ItemItemAtlas to read bytes and upload to the GPU
//The PutBakedQuad static method accepts any IVertexConsumer, writing either to VertexConsumer3D itself or to StagedVertexBuffer.VertexBuilder
//Vertex format position(3f)+color(1f)+uv(2f)+light(1f)+normal(3f), a simplification of vanilla without overlay
//color/light are ints written as converted floats and restored with int() in the shader; 0xFFFFFFFF's bit pattern is NaN so a bit-pattern conversion cannot be used
public sealed class VertexConsumer3D : IVertexConsumer
{
    //Vertices written vertex data, 10 floats per vertex position3+color1+uv2+light1+normal3
    //The P14 chain uses MemoryMarshal.AsBytes to memcpy directly for GPU upload
    public readonly List<float> Vertices = new();

    //AddVertex3D implements IVertexConsumer to write into List<float>
    //position and normal are pre-transformed by PutBakedQuad; color/light are written as converted values
    public void AddVertex3D(float x, float y, float z, int color,
        float u, float v, int light, float nx, float ny, float nz)
    {
        //position3
        Vertices.Add(x); Vertices.Add(y); Vertices.Add(z);
        //color converted as a number 0xFFFFFFFF -> -1.0f shader int(-1.0f)=-1=0xFFFFFFFF
        Vertices.Add((float)color);
        //uv2
        Vertices.Add(u); Vertices.Add(v);
        //light converted as a number; light values within 2^24 are exactly representable as float
        Vertices.Add((float)light);
        //normal3
        Vertices.Add(nx); Vertices.Add(ny); Vertices.Add(nz);
    }

    //PutBakedQuad transforms the BakedQuad's 4 vertices with PoseStack then writes them to the consumer
    //position transformed by pose normal transformed by normalMatrix then normalized
    //color/light come from QuadInstance with emission merged into the blockLight field
    public static void PutBakedQuad(IVertexConsumer consumer, PoseStack poseStack, in BakedQuad quad, QuadInstance instance)
    {
        var pose = poseStack.Pose();
        var normalMatrix = poseStack.Normal();
        var normalVec = quad.Direction.UnitVector();
        var normal = Vector3.Normalize(Vector3.TransformNormal(normalVec, normalMatrix));
        var lightEmission = quad.LightEmission;
        for (var i = 0; i < BakedQuad.VertexCount; i++)
        {
            var pos = Vector3.Transform(quad.Position(i), pose);
            var uv = quad.Uv(i);
            var color = instance.GetColor(i);
            var light = instance.GetLightCoordsWithEmission(i, lightEmission);
            consumer.AddVertex3D(pos.X, pos.Y, pos.Z, color, uv.X, uv.Y, light, normal.X, normal.Y, normal.Z);
        }
    }

    public int VertexCount => Vertices.Count / 10;
    public void Clear() => Vertices.Clear();

    //AddVertexWith2DPose 2D vertices are unsupported by VertexConsumer3D and explicitly throw
    void IVertexConsumer.AddVertexWith2DPose(Matrix3x2 pose, float x, float y, float u, float v, int color)
        => throw new NotSupportedException("VertexConsumer3D does not support 2D vertices");
}
