using NetCraft.Codec;
using NetCraft.Registry;

namespace NetCraft.Storage;

//ValueInput NBT 读取抽象对应原版 net.minecraft.world.level.storage.ValueInput
//提供按字段名读标量与子节点与列表的入口
//简化点用 T? 替代 Optional<T> 不实现完整 ProblemReporter
public interface ValueInput
{
    //按 Codec 从字段名解析值缺失返回 null
    T? Read<T>(string name, Codec<T> codec);

    //取子节点缺失返回 null
    ValueInput? Child(string name);

    //取子节点缺失返回空 ValueInput
    ValueInput ChildOrEmpty(string name);

    //取子节点列表缺失返回 null
    IReadOnlyList<ValueInput>? ChildrenList(string name);

    //取子节点列表缺失返回空列表
    IReadOnlyList<ValueInput> ChildrenListOrEmpty(string name);

    //按 Codec 列表解析缺失返回 null
    IReadOnlyList<T>? List<T>(string name, Codec<T> codec);

    //按 Codec 列表解析缺失返回空列表
    IReadOnlyList<T> ListOrEmpty<T>(string name, Codec<T> codec);

    bool GetBooleanOr(string name, bool defaultValue);
    byte GetByteOr(string name, byte defaultValue);
    int GetShortOr(string name, short defaultValue);
    int? GetInt(string name);
    int GetIntOr(string name, int defaultValue);
    long GetLongOr(string name, long defaultValue);
    long? GetLong(string name);
    float GetFloatOr(string name, float defaultValue);
    double GetDoubleOr(string name, double defaultValue);
    string? GetString(string name);
    string GetStringOr(string name, string defaultValue);
    int[]? GetIntArray(string name);

    //注册表访问入口用于 Codec 解析时查表
    RegistryAccess Lookup();

    bool IsEmpty();
}
