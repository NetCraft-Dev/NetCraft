using System.Numerics;

namespace NetCraft.Gpu;

//ItemFeatureRenderer 物品特性渲染器对标原版 ItemFeatureRenderer
//Execute 遍历 ItemSubmitCollector 的节点用临时 PoseStack 重建 pose 后 putBakedQuad 写到 VertexConsumer
//原版分 prepare/execute 两阶段 + phase 分组 PoC 简化为单阶段直接写
//FULL_BRIGHT 全亮光坐标 GUI 物品用 NO_OVERLAY 无 overlay
public static class ItemFeatureRenderer
{
    //全亮光坐标 blocklight=15 skylight=15 packed
    public const int FullBright = 0x00F000F0;
    //无 overlay
    public const int NoOverlay = 0;

    //Execute 把 collector 的所有 submit node 渲染到 IVertexConsumer
    //每个 node 用其 pose 快照重建临时 PoseStack 再 PutBakedQuad
    //tint 颜色按 quad.TintIndex 查 ItemTints.GetTint 应用到顶点 color 字段
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
