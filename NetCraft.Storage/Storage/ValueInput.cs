using NetCraft.Codec;
using NetCraft.Registry;

namespace NetCraft.Storage;

//ValueInput, NBT read abstraction, maps to vanilla net.minecraft.world.level.storage.ValueInput
//Provides entry points to read scalars, children and lists by field name
//Simplification: T? replaces Optional<T> and no full ProblemReporter is implemented
public interface ValueInput
{
    //Parse the value from the field name with a Codec; returns null when missing
    T? Read<T>(string name, Codec<T> codec);

    //Get a child node; returns null when missing
    ValueInput? Child(string name);

    //Get a child node; returns an empty ValueInput when missing
    ValueInput ChildOrEmpty(string name);

    //Get a child node list; returns null when missing
    IReadOnlyList<ValueInput>? ChildrenList(string name);

    //Get a child node list; returns an empty list when missing
    IReadOnlyList<ValueInput> ChildrenListOrEmpty(string name);

    //Parse a list with a Codec; returns null when missing
    IReadOnlyList<T>? List<T>(string name, Codec<T> codec);

    //Parse a list with a Codec; returns an empty list when missing
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

    //Registry access entry point, used by Codec parsing for lookups
    RegistryAccess Lookup();

    bool IsEmpty();
}
