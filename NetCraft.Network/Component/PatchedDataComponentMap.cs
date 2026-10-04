using NetCraft.Codec;
using NetCraft.Registry;

namespace NetCraft.Network.Component;

//PatchedDataComponentMap 应用 patch 的可读组件映射对应原版 net.minecraft.core.component.PatchedDataComponentMap
//prototype 提供基础值 patch 覆盖或移除 get 时先查 patch 再回退 prototype
//keySet 为 prototype 全集减 patch 移除加 patch 新增
public sealed class PatchedDataComponentMap : DataComponentMap
{
    private readonly DataComponentMap _prototype;
    private readonly DataComponentPatch _patch;

    public PatchedDataComponentMap(DataComponentMap prototype, DataComponentPatch patch)
    {
        _prototype = prototype;
        _patch = patch;
    }

    //Patch 当前应用的补丁
    public DataComponentPatch Patch => _patch;

    //Get 先查 patch present 返回值 empty 返回 null 移除 不在 patch 回退 prototype
    public T? Get<T>(DataComponentType<T> type) where T : class
    {
        var optional = _patch.Get(type);
        if (optional.HasValue)
        {
            return optional.Value.IsPresent ? (T)optional.Value.Get() : null;
        }
        return _prototype.Get(type);
    }

    //KeySet prototype 全集减 patch 移除加 patch 新增
    public IEnumerable<object> KeySet
    {
        get
        {
            var removed = new HashSet<object>();
            var added = new HashSet<object>();
            foreach (var kv in _patch.AsMap())
            {
                if (kv.Value.IsPresent)
                    added.Add(kv.Key);
                else
                    removed.Add(kv.Key);
            }
            foreach (var key in _prototype.KeySet)
            {
                if (!removed.Contains(key))
                    yield return key;
            }
            foreach (var key in added)
            {
                yield return key;
            }
        }
    }

    //AsPatch 返回 patch 供 ItemStack.STREAM_CODEC 编码
    public DataComponentPatch AsPatch() => _patch;
}
