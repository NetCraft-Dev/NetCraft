namespace NetCraft.Registry;

//Fluid 抽象基类对应原版 net.minecraft.world.level.material.Fluid
//原版继承 FlowingFluid 此处简化为抽象类
//默认 FluidState 占位待 FluidState 子系统就绪
public abstract class Fluid
{
    //Id 流体的注册名子类必须实现
    public abstract Identifier Id { get; }
}
