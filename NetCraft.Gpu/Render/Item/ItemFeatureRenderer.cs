using System.Numerics;

namespace NetCraft.Gpu;

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
