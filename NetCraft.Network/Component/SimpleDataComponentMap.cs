using NetCraft.Registry;

namespace NetCraft.Network.Component;

//SimpleDataComponentMap DataComponentMap 简单实现对应原版 DataComponentMap.Builder.ImmutableMap
//内部用 Dictionary<object,object> 存 key 是 DataComponentType 实例 value 是装箱值
//Get 按 type 引用查找 cast 为 T 调用方保证 type 与存入时同一实例
public sealed class SimpleDataComponentMap : DataComponentMap
{
    private readonly Dictionary<object, object> _map;

    public SimpleDataComponentMap(Dictionary<object, object> map)
    {
        _map = map;
    }

    public T? Get<T>(DataComponentType<T> type) where T : class
        => _map.TryGetValue(type, out var value) ? (T)value : null;

    public IEnumerable<object> KeySet => _map.Keys;
}

//DataComponentMapBuilder DataComponentMap 构造器对应原版 DataComponentMap.Builder
//add 按 TypedDataComponent 添加 set 按 type+value 添加 remove 按 type 删除
//build 返回不可变 SimpleDataComponentMap
public sealed class DataComponentMapBuilder
{
    private readonly Dictionary<object, object> _map = new();

    //Add 按 TypedDataComponent 添加
    public DataComponentMapBuilder Add<T>(TypedDataComponent<T> component) where T : class
    {
        _map[component.Type] = component.Value;
        return this;
    }

    //Set 按 type+value 添加
    public DataComponentMapBuilder Set<T>(DataComponentType<T> type, T value) where T : class
    {
        _map[type] = value;
        return this;
    }

    //Remove 按 type 删除
    public DataComponentMapBuilder Remove<T>(DataComponentType<T> type) where T : class
    {
        _map.Remove(type);
        return this;
    }

    //Build 返回 SimpleDataComponentMap
    public DataComponentMap Build()
        => new SimpleDataComponentMap(new Dictionary<object, object>(_map));

    //BuildFromMapTrusted 从已有 Dictionary 构建信任输入不做拷贝
    public static DataComponentMap BuildFromMapTrusted(Dictionary<object, object> map)
        => new SimpleDataComponentMap(map);
}
