using NetCraft.Codec;

namespace NetCraft.Util.Collection;

//Optional工具对应原版net.minecraft.util.Util.ifElse
public static class OptionalUtil
{
    //ifElse按Optional是否present分支调用对应原版Util.ifElse
    //返回原Optional便于链式调用
    public static Optional<T> IfElse<T>(Optional<T> input, Action<T> onTrue, Action onFalse)
    {
        if (input.IsPresent)
            onTrue(input.Get());
        else
            onFalse();
        return input;
    }
}
