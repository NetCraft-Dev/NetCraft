namespace NetCraft.Storage.Paletted;

//调色板接口对应原版net.minecraft.world.level.chunk.Palette
//管理storage的int id与T值的映射
public interface Palette<T>
{
    //返回value对应的id不存在则通过resizeHandler扩容对应原版idFor
    int IdFor(T value, PaletteResize<T> resizeHandler);

    //是否存在满足predicate的值对应原版maybeHas
    bool MaybeHas(Predicate<T> predicate);

    //根据id获取值对应原版valueFor
    T ValueFor(int index);

    //palette中实际条目数对应原版getSize
    int Size { get; }

    Palette<T> Copy();
}
