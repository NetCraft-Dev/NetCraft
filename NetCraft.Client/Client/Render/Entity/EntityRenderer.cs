using NetCraft.Gpu;
using NetCraft.Gpu.Pipeline;

namespace NetCraft.Game.Client.Render.Entity;

//EntityRenderer 实体渲染器基类对标原版 net.minecraft.client.renderer.entity.EntityRenderer
//子类持 EntityModel 实现 Render 生成顶点每实体类型一个 Renderer 实例
//shadowRadius/shadowStrength 阴影参数 PoC 骨架版不渲染阴影 W9.2 接入
public abstract class EntityRenderer
{
    //Model 实体模型子类提供
    public abstract EntityModel Model { get; }

    //Pipeline 渲染管线默认取 Model.Pipeline 子类可覆盖
    public virtual RenderPipeline Pipeline => Model.Pipeline;

    //ShadowRadius 阴影半径 0 表示无阴影
    public virtual float ShadowRadius => 0f;

    //ShadowStrength 阴影强度
    public virtual float ShadowStrength => 1f;

    //Render 生成实体顶点写入 builder
    //poseStack 已含相机投影外的所有变换调用方负责 push 实体世界变换
    //state 实体渲染状态含位置朝向动画光照
    //overlayCoords overlay 坐标默认 0 无伤害红闪
    //color 颜色乘数默认 -1 白色不调制对标原版
    public virtual void Render(PoseStack poseStack, EntityVertexBuilder builder, EntityRenderState state)
    {
        Model.SetupAnim(state);
        //光照坐标从 state 读取由调用方按实体所在位置光等级填充 默认 FullBright
        Model.RenderToBuffer(poseStack, builder, state.LightCoords, 0, -1);
    }
}
