using NetCraft.Gpu.Pipeline;

namespace NetCraft.Gpu;

//EntityModel 实体模型基类对标原版 net.minecraft.client.model.EntityModel
//持 root ModelPart 子类在构造时构建模型树 setupAnim 驱动动画
//renderToBuffer 遍历 ModelPart 树生成顶点写入 EntityVertexBuilder
//Pipeline 属性指定渲染管线默认 ENTITY_CUTOUT 子类可覆盖
public abstract class EntityModel
{
    //Root 模型根节点子类在构造时填充
    public ModelPart Root { get; }
    //Pipeline 渲染管线默认 ENTITY_CUTOUT 子类可覆盖返回 SOLID/TRANSLUCENT
    public virtual RenderPipeline Pipeline => EntityRenderPipelines.ENTITY_CUTOUT;

    protected EntityModel(ModelPart root) => Root = root;

    //SetupAnim 动画驱动子类重写按 state 调整 ModelPart 旋转
    //PoC 骨架版空实现 W9.2 接入真实动画
    public virtual void SetupAnim(EntityRenderState state) { }

    //RenderToBuffer 渲染模型树到 builder
    //poseStack 已含实体世界变换（位置+朝向）light/overlay/color 由 EntityRenderer 传入
    public void RenderToBuffer(PoseStack poseStack, EntityVertexBuilder builder, int lightCoords, int overlayCoords, int color)
    {
        Root.Render(poseStack, builder, lightCoords, overlayCoords, color);
    }
}
