using System.Numerics;

namespace NetCraft.Gpu;

//EntityRenderState 实体渲染状态对标原版 EntityRenderState
//持有实体渲染所需的位置朝向动画光照等数据由 EntityRenderer 从 Entity 提取填充
//W9.2 扩展动画输入 AgeInTicks/WalkAnimation 与 LightCoords 真实光照
public sealed class EntityRenderState
{
    //Position 实体世界位置
    public Vector3 Position { get; set; }
    //YRot 实体 Y 轴旋转弧度朝向
    public float YRot { get; set; }
    //XRot 实体 X 轴旋转弧度俯仰
    public float XRot { get; set; }
    //YBodyRot 身体朝向弧度 与 YRot 分离让头部可相对身体独立转动
    public float YBodyRot { get; set; }
    //AgeInTicks 实体存活 tick 数驱动 idle 动画对标原版 ageInTicks
    public float AgeInTicks { get; set; }
    //BobOffset 上下浮动相位对标原版 ItemEntity 的 bobOffs 由实体 id 派生保证稳定
    public float BobOffset { get; set; }
    //WalkAnimationPos 行走动画相位(limbSwing) 由移动距离累计对标原版 walkAnimationPos
    public float WalkAnimationPos { get; set; }
    //WalkAnimationSpeed 行走动画速度(limbSwingAmount) 0-1 移动强度对标原版 walkAnimationSpeed
    public float WalkAnimationSpeed { get; set; }
    //LightCoords 光照坐标 packed int 由调用方按实体所在位置光等级填充
    //默认全亮兼容未接光照的调用方
    public int LightCoords { get; set; } = LightTexture.FullBrightCoords;
    //Name 实体名称用于调试
    public string Name { get; set; } = string.Empty;
    //CustomData 渲染器专属数据由具体 EntityRenderer 自行解释
    //Gpu 层不认识业务类型 掉落物的 ItemStack 走这里传给 ItemEntityRenderer
    public object? CustomData { get; set; }
}
