using NetCraft.Codec;
using NetCraft.Logging;
using NetCraft.Nbt;

namespace NetCraft.Storage;

//TagValueOutput, a CompoundTag-based ValueOutput implementation, maps to vanilla TagValueOutput
//Simplification: no ProblemReporter; error paths go to Log.Warning and partial values are ignored
public sealed class TagValueOutput : ValueOutput
{
    private readonly CompoundTag _output;

    public TagValueOutput() : this(new CompoundTag()) { }

    private TagValueOutput(CompoundTag output) => _output = output;

    //Serialize a value into a field with a Codec, maps to vanilla store
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

    //Serialize a nullable value into a field with a Codec; null is skipped
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

    //Create a child node output, maps to vanilla child
    public ValueOutput Child(string name)
    {
        var child = new CompoundTag();
        _output.Put(name, child);
        return new TagValueOutput(child);
    }

    //Create a child node list output, maps to vanilla childrenList
    public ValueOutput.ValueOutputList ChildrenList(string name)
    {
        var list = new ListTag();
        _output.Put(name, list);
        return new ListWrapper(list);
    }

    //Create a typed list output with a Codec, maps to vanilla list
    public ValueOutput.TypedOutputList<T> List<T>(string name, Codec<T> codec)
    {
        var list = new ListTag();
        _output.Put(name, list);
        return new TypedListWrapper<T>(codec, list);
    }

    public void Discard(string name) => _output.Remove(name);

    public bool IsEmpty() => _output.IsEmpty;

    //BuildResult returns the internal CompoundTag
    public CompoundTag BuildResult() => _output;

    //ListWrapper, child node list implementation, maps to vanilla TagValueOutput.ListWrapper
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

    //TypedListWrapper, typed list implementation, maps to vanilla TagValueOutput.TypedListWrapper
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
