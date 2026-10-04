using NetCraft.Registry;

namespace NetCraft.Game.World.Items;

//FilterMask 组件过滤掩码对应原版 net.minecraft.world.item.component.FilterMask
//用于 Container 型 codec 如潜影盒序列化时按 type 引用过滤组件
//默认全部通过显式 Add 排除显式 Remove 包含
//Container 物品接入时用 FilterMask.Filter 限定暴露给客户端的组件子集
public sealed class FilterMask
{
    //Exclusion 显式排除的 type 引用集合对齐原版 exclusionMask
    private readonly HashSet<object> _exclusion = new(ReferenceEqualityComparer.Instance);
    //Inclusion 显式包含的 type 引用集合对齐原版 inclusionMask
    private readonly HashSet<object> _inclusion = new(ReferenceEqualityComparer.Instance);

    public FilterMask() { }

    //IsEmpty 无任何掩码规则全部组件按默认通过
    public bool IsEmpty => _exclusion.Count == 0 && _inclusion.Count == 0;

    //Add 把 type 加入排除集合对应原版 add
    //默认通过 Add 后变为排除
    public void Add<T>(DataComponentType<T> type) where T : class
        => _exclusion.Add(type);

    //Remove 把 type 从排除集合移除并加入包含集合对应原版 remove
    //默认通过 Remove 后仍为通过但被反向标记为显式包含
    public void Remove<T>(DataComponentType<T> type) where T : class
    {
        _exclusion.Remove(type);
        _inclusion.Add(type);
    }

    //IsFiltered 判断 type 是否被过滤掉
    //排除集合命中返回 true 否则 false
    public bool IsFiltered<T>(DataComponentType<T> type) where T : class
        => IsFilteredObject(type);

    //IsFilteredObject 非泛型版供 PredicateDataComponentMap 内 Func<object,bool> 用
    public bool IsFilteredObject(object type)
        => _exclusion.Contains(type);

    //IsExplicitlyIncluded 判断 type 是否被显式标记为包含
    //用于 Container codec 决定是否强制保留某些组件
    public bool IsExplicitlyIncluded<T>(DataComponentType<T> type) where T : class
        => _inclusion.Contains(type);

    //Filter 按掩码过滤 DataComponentMap 返回新 map 只保留未排除的组件
    //对齐原版 filter 应用排除规则后产出新的 DataComponentMap
    public DataComponentMap Filter(DataComponentMap map)
    {
        if (IsEmpty) return map;
        //用 PredicateDataComponentMap 包装原 map 按掩码过滤避免拷贝
        return new PredicateDataComponentMap(map, IsFilteredObject);
    }

    //PredicateDataComponentMap 包装原 map 按谓词过滤的视图
    //Get 走原 map 但被过滤的 type 返回 null
    private sealed class PredicateDataComponentMap : DataComponentMap
    {
        private readonly DataComponentMap _delegate;
        private readonly Func<object, bool> _isFiltered;

        public PredicateDataComponentMap(DataComponentMap map, Func<object, bool> isFiltered)
        {
            _delegate = map;
            _isFiltered = isFiltered;
        }

        public T? Get<T>(DataComponentType<T> type) where T : class
            => _isFiltered(type) ? null : _delegate.Get(type);

        public IEnumerable<object> KeySet
            => _delegate.KeySet.Where(t => !_isFiltered(t));
    }
}
