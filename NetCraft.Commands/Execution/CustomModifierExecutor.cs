using NetCraft.Commands.Context;

namespace NetCraft.Commands.Execution;

//CustomModifierExecutor 自定义修饰器对应原版 net.minecraft.commands.execution.CustomModifierExecutor
//修饰器阶段不展开源集合而是把链与控制权交给实现 自己决定续跑什么
//原版另有 ModifierAdapter 桥到 RedirectModifier C# 侧修饰器是委托 桥接交给注册侧的闭包
public interface CustomModifierExecutor<T>
{
    //Apply 应用修饰
    void Apply(T originalSource, List<T> sources, ContextChain<T> chain, ChainModifiers modifiers, ExecutionControl<T> output);
}
