using NetCraft.Codec;
using NetCraft.Logging;
using NetCraft.Nbt;

namespace NetCraft.Storage;

//TagValueOutput 基于 CompoundTag 的 ValueOutput 实现对应原版 TagValueOutput
//简化点不实现 ProblemReporter 错误路径走 Log.Warning 部分值忽略
public sealed class TagValueOutput : ValueOutput
{
    private readonly CompoundTag _output;

    public TagValueOutput() : this(new CompoundTag()) { }

    private TagValueOutput(CompoundTag output) => _output = output;

    //按 Codec 把值序列化到字段对应原版 store
    public void Store<T>(string name, Codec<T> codec, T value)
    {
        var result = codec.EncodeStart(NbtOps.Instance, value);
        if (result.Result().IsPresent)
        {
            _output.Put(name, result.GetOrThrow());
        }
        else
        {
            Log.Warning($"TagValueOutput.Store {name} encode failed");
        }
    }

    //按 Codec 把可空值序列化到字段 null 跳过
    public void StoreNullable<T>(string name, Codec<T> codec, T? value) where T : class
    {
        if (value is not null) Store(name, codec, value);
    }

    public void PutBoolean(string name, bool value) => _output.PutBoolean(name, value);
    public void PutByte(string name, byte value) => _output.PutByte(name, value);
    public void PutShort(string name, short value) => _output.PutShort(name, value);
    public void PutInt(string name, int value) => _output.PutInt(name, value);
    public void PutLong(string name, long value) => _output.PutLong(name, value);
    public void PutFloat(string name, float value) => _output.PutFloat(name, value);
    public void PutDouble(string name, double value) => _output.PutDouble(name, value);
    public void PutString(string name, string value) => _output.PutString(name, value);
    public void PutIntArray(string name, int[] value) => _output.PutIntArray(name, value);

    //创建子节点输出对应原版 child
    public ValueOutput Child(string name)
    {
        var child = new CompoundTag();
        _output.Put(name, child);
        return new TagValueOutput(child);
    }

    //创建子节点列表输出对应原版 childrenList
    public ValueOutput.ValueOutputList ChildrenList(string name)
    {
        var list = new ListTag();
        _output.Put(name, list);
        return new ListWrapper(list);
    }

    //按 Codec 创建类型化列表输出对应原版 list
    public ValueOutput.TypedOutputList<T> List<T>(string name, Codec<T> codec)
    {
        var list = new ListTag();
        _output.Put(name, list);
        return new TypedListWrapper<T>(codec, list);
    }

    public void Discard(string name) => _output.Remove(name);

    public bool IsEmpty() => _output.IsEmpty;

    //构建结果返回内部 CompoundTag
    public CompoundTag BuildResult() => _output;

    //ListWrapper 子节点列表实现对应原版 TagValueOutput.ListWrapper
    private sealed class ListWrapper : ValueOutput.ValueOutputList
    {
        private readonly ListTag _list;

        public ListWrapper(ListTag list) => _list = list;

        public ValueOutput AddChild()
        {
            var child = new CompoundTag();
            _list.Add(child);
            return new TagValueOutput(child);
        }

        public void DiscardLast() => _list.RemoveLast();

        public bool IsEmpty() => _list.IsEmpty;
    }

    //TypedListWrapper 类型化列表实现对应原版 TagValueOutput.TypedListWrapper
    private sealed class TypedListWrapper<T> : ValueOutput.TypedOutputList<T>
    {
        private readonly Codec<T> _codec;
        private readonly ListTag _list;

        public TypedListWrapper(Codec<T> codec, ListTag list)
        {
            _codec = codec;
            _list = list;
        }

        public void Add(T value)
        {
            var result = _codec.EncodeStart(NbtOps.Instance, value);
            if (result.Result().IsPresent) _list.Add(result.GetOrThrow());
            else Log.Warning("TagValueOutput.TypedListWrapper.Add encode failed");
        }

        public bool IsEmpty() => _list.IsEmpty;
    }
}
