using NetCraft.Codec;
using NetCraft.Registry;
using System.Text;

namespace NetCraft.Network.Component;

//PatchedDataComponentMap 应用 patch 的可读组件映射对应原版 net.minecraft.core.component.PatchedDataComponentMap
//prototype 提供基础值 patch 覆盖或移除 get 时先查 patch 再回退 prototype
//keySet 为 prototype 全集减 patch 移除加 patch 新增
//改写走不可变 patch 每次重建一份 与原版的 copyOnWrite 可变补丁等价
public sealed class PatchedDataComponentMap : DataComponentMap
{
    private readonly DataComponentMap _prototype;
    private DataComponentPatch _patch;

    public PatchedDataComponentMap(DataComponentMap prototype)
        : this(prototype, DataComponentPatch.Empty)
    {
    }

    public PatchedDataComponentMap(DataComponentMap prototype, DataComponentPatch patch)
    {
        _prototype = prototype;
        _patch = patch;
    }

    //FromPatch 从原型与补丁构造 补丁里与原型等值的项会被清掉 对应原版 fromPatch
    public static PatchedDataComponentMap FromPatch(DataComponentMap prototype, DataComponentPatch patch)
    {
        var map = new PatchedDataComponentMap(prototype);
        map.ApplyPatch(patch);
        return map;
    }

    //Prototype 基础映射
    public DataComponentMap Prototype => _prototype;

    //Patch 当前应用的补丁
    public DataComponentPatch Patch => _patch;

    //Get 先查 patch present 返回值 empty 返回 null 移除 不在 patch 回退 prototype
    public T? Get<T>(DataComponentType<T> type) where T : class => _patch.GetFrom(_prototype, type);

    //HasNonDefault 该项在补丁里被显式改过 对应原版 hasNonDefault
    public bool HasNonDefault<T>(DataComponentType<T> type) where T : class => _patch.Get(type) is not null;

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

    //Set 覆写一个组件值 写回原型同值等价于撤销该项 返回改写前的值 对应原版 set
    public T? Set<T>(DataComponentType<T> type, T value) where T : class
    {
        var previous = Get(type);
        var map = new Dictionary<object, Optional<object>>(_patch.AsMap());
        if (Equals(value, _prototype.Get(type))) map.Remove(type);
        else map[type] = Optional<object>.Of(value);
        _patch = map.Count == 0 ? DataComponentPatch.Empty : new DataComponentPatch(map);
        return previous;
    }

    //Set 按带类型的组件条目写入
    public T? Set<T>(TypedDataComponent<T> component) where T : class => Set(component.Type, component.Value);

    //Remove 移除一个组件 原型里没有该项时不往补丁里留痕迹 返回移除前的值 对应原版 remove
    public T? Remove<T>(DataComponentType<T> type) where T : class
    {
        var previous = Get(type);
        var map = new Dictionary<object, Optional<object>>(_patch.AsMap());
        if (_prototype.Get(type) is not null) map[type] = Optional<object>.Empty();
        else map.Remove(type);
        _patch = map.Count == 0 ? DataComponentPatch.Empty : new DataComponentPatch(map);
        return previous;
    }

    //ApplyPatch 逐项应用补丁 与原型等值的项不留在补丁里 对应原版 applyPatch
    public void ApplyPatch(DataComponentPatch patch)
    {
        if (patch.IsEmpty) return;
        var map = new Dictionary<object, Optional<object>>(_patch.AsMap());
        foreach (var kv in patch.AsMap())
        {
            var prototypeValue = kv.Key is DataComponentType<object> type ? _prototype.Get(type) : null;
            if (kv.Value.IsPresent)
            {
                if (Equals(kv.Value.Get(), prototypeValue)) map.Remove(kv.Key);
                else map[kv.Key] = kv.Value;
            }
            else
            {
                if (prototypeValue is not null) map[kv.Key] = Optional<object>.Empty();
                else map.Remove(kv.Key);
            }
        }
        _patch = map.Count == 0 ? DataComponentPatch.Empty : new DataComponentPatch(map);
    }

    //RestorePatch 丢弃当前补丁整体换成给定补丁 对应原版 restorePatch
    public void RestorePatch(DataComponentPatch patch) => _patch = patch;

    //ClearPatch 清空补丁回到纯原型 对应原版 clearPatch
    public void ClearPatch() => _patch = DataComponentPatch.Empty;

    //SetAll 把另一份映射的组件逐个盖上来 对应原版 setAll
    public void SetAll(DataComponentMap components)
    {
        foreach (var key in components.KeySet)
            if (key is DataComponentType<object> type && components.Get(type) is { } value)
                Set(type, value);
    }

    //AsPatch 返回 patch 供 ItemStack.STREAM_CODEC 编码
    public DataComponentPatch AsPatch() => _patch;

    //Copy 复制一份本映射 补丁是不可变的直接共用
    public PatchedDataComponentMap Copy() => new(_prototype, _patch);

    //ToImmutableMap 无补丁时原型本身即结果 对应原版 toImmutableMap
    public DataComponentMap ToImmutableMap() => _patch.IsEmpty ? _prototype : Copy();

    //判等要求原型与补丁都相同 对应原版 equals
    public override bool Equals(object? obj)
        => ReferenceEquals(this, obj)
            || (obj is PatchedDataComponentMap other
                && Equals(_prototype, other._prototype)
                && _patch.Equals(other._patch));

    //哈希与判等一致
    public override int GetHashCode() => _prototype.GetHashCode() + (_patch.GetHashCode() * 31);

    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append('{');
        var first = true;
        foreach (var key in KeySet)
        {
            if (!first) sb.Append(", ");
            first = false;
            if (key is DataComponentType<object> type && Get(type) is { } value)
                sb.Append(key).Append("=>").Append(value);
        }
        sb.Append('}');
        return sb.ToString();
    }
}
