using NetCraft.Codec;

namespace NetCraft.Util.Collection;

//Optional helper, maps to vanilla net.minecraft.util.Util.ifElse
public static class OptionalUtil
{
    //ifElse branches on whether the Optional is present, maps to vanilla Util.ifElse
    //Returns the original Optional for chaining
    public static Optional<T> IfElse<T>(Optional<T> input, Action<T> onTrue, Action onFalse)
    {
        if (input.IsPresent)
            onTrue(input.Get());
        else
            onFalse();
        return input;
    }
}
