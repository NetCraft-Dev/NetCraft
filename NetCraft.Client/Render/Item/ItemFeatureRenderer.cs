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

//ItemFeatureRenderer item feature renderer, maps to vanilla ItemFeatureRenderer
//Execute walks the ItemSubmitCollector nodes, rebuilding the pose with a temporary PoseStack then putBakedQuad into the VertexConsumer
//Vanilla splits into prepare/execute stages + phase groups; the PoC simplifies to one stage writing directly
//FULL_BRIGHT full-bright light coordinates GUI items use NO_OVERLAY, no overlay
public static class ItemFeatureRenderer
{
    //Full-bright light coordinates blocklight=15 skylight=15 packed
    public const int FullBright = 0x00F000F0;
    //No overlay
    public const int NoOverlay = 0;

    //Execute renders all the collector's submit nodes to the IVertexConsumer
    //Each node rebuilds a temporary PoseStack from its pose snapshot, then PutBakedQuad
    //The tint color is looked up by quad.TintIndex via ItemTints.GetTint and applied to the vertex color field
    public static void Execute(ItemSubmitCollector collector, IVertexConsumer consumer)
    {
        var tempPose = new PoseStack();
        var instance = new QuadInstance();
        foreach (var node in collector.Nodes)
        {
            tempPose.SetIdentity();
            tempPose.MulPose(node.Pose);
            instance.LightCoords = node.LightCoords;
            instance.OverlayCoords = node.OverlayCoords;
            foreach (var quad in node.Quads)
            {
                instance.Color = ItemTints.GetTint(quad.TintIndex);
                VertexConsumer3D.PutBakedQuad(consumer, tempPose, in quad, instance);
            }
        }
    }
}
