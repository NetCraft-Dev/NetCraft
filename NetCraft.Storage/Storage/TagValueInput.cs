using NetCraft.Codec;
using NetCraft.Logging;
using NetCraft.Nbt;
using NetCraft.Registry;

namespace NetCraft.Storage;

//TagValueInput 基于 CompoundTag 的 ValueInput 实现对应原版 TagValueInput
//简化点不实现 ProblemReporter 错误路径走 Log.Warning
public sealed class TagValueInput : ValueInput
{
    private readonly RegistryAccess _registryAccess;
    private readonly CompoundTag _input;

    public TagValueInput(RegistryAccess registryAccess, CompoundTag input)
    {
        _registryAccess = registryAccess;
        _input = input;
    }

    public static TagValueInput Create(RegistryAccess registryAccess, CompoundTag tag)
        => new(registryAccess, tag);

    //按 Codec 从字段解析值缺失返回 null 解析错误走 Log.Warning 返回 null
    public T? Read<T>(string name, Codec<T> codec)
    {
        var tag = _input[name];
        if (tag is null) return default;
        var result = codec.Parse(NbtOps.Instance, tag);
        if (!result.Result().IsPresent)
        {
            Log.Warning($"TagValueInput.Read {name} parse failed");
            return default;
        }
        return result.GetOrThrow();
    }

    public ValueInput? Child(string name)
    {
        var child = _input.GetCompound(name);
        return child is null || child.IsEmpty ? null : new TagValueInput(_registryAccess, child);
    }

    public ValueInput ChildOrEmpty(string name)
    {
        var child = _input.GetCompound(name);
        return child is null ? Empty(_registryAccess) : new TagValueInput(_registryAccess, child);
    }

    public IReadOnlyList<ValueInput>? ChildrenList(string name)
    {
        var list = _input.GetList(name);
        if (list is null) return null;
        var result = new List<ValueInput>(list.Count);
        foreach (var t in list)
            if (t is CompoundTag c) result.Add(new TagValueInput(_registryAccess, c));
        return result;
    }

    public IReadOnlyList<ValueInput> ChildrenListOrEmpty(string name)
        => ChildrenList(name) ?? Array.Empty<ValueInput>();

    public IReadOnlyList<T>? List<T>(string name, Codec<T> codec)
    {
        var list = _input.GetList(name);
        if (list is null) return null;
        var result = new List<T>(list.Count);
        foreach (var t in list)
        {
            var parsed = codec.Parse(NbtOps.Instance, t);
            if (parsed.Result().IsPresent) result.Add(parsed.GetOrThrow());
        }
        return result;
    }

    public IReadOnlyList<T> ListOrEmpty<T>(string name, Codec<T> codec)
        => List<T>(name, codec) ?? Array.Empty<T>();

    public bool GetBooleanOr(string name, bool defaultValue) => _input.GetBooleanOr(name, defaultValue);

    public byte GetByteOr(string name, byte defaultValue) => _input.GetByteOr(name, defaultValue);

    public int GetShortOr(string name, short defaultValue)
    {
        var tag = _input.GetShort(name);
        return tag is null ? defaultValue : tag.Value;
    }

    public int? GetInt(string name)
    {
        var tag = _input.GetInt(name);
        return tag is null ? null : tag.Value;
    }

    public int GetIntOr(string name, int defaultValue) => _input.GetIntOr(name, defaultValue);

    public long GetLongOr(string name, long defaultValue)
    {
        var tag = _input.GetLong(name);
        return tag is null ? defaultValue : tag.Value;
    }

    public long? GetLong(string name)
    {
        var tag = _input.GetLong(name);
        return tag is null ? null : tag.Value;
    }

    public float GetFloatOr(string name, float defaultValue)
    {
        var tag = _input.GetFloat(name);
        return tag is null ? defaultValue : tag.Value;
    }

    public double GetDoubleOr(string name, double defaultValue)
    {
        var tag = _input.GetDouble(name);
        return tag is null ? defaultValue : tag.Value;
    }

    public string? GetString(string name)
    {
        var tag = _input.GetString(name);
        return tag?.Value;
    }

    public string GetStringOr(string name, string defaultValue)
    {
        var tag = _input.GetString(name);
        return tag is null ? defaultValue : tag.Value;
    }

    public int[]? GetIntArray(string name) => _input.GetIntArray(name)?.Value;

    public RegistryAccess Lookup() => _registryAccess;

    public bool IsEmpty() => _input.IsEmpty;

    //空 ValueInput 单例用于 ChildOrEmpty 缺失场景
    public static TagValueInput Empty(RegistryAccess registryAccess)
        => new(registryAccess, new CompoundTag());
}
