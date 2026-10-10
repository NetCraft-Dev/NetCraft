using NetCraft.Codec;
using NetCraft.Registry;

namespace NetCraft.Registry;

//TagEntry tag entry, maps to vanilla net.minecraft.tags.TagEntry
//Represents an entry in a tag, either an element reference or a tag reference, each with a required flag
//The serialized format aligns with the vanilla string prefix encoding
//   #id optional tag    !#id required tag    !id required element    id optional element
public sealed record TagEntry(Identifier EntryId, bool Required, bool IsTag)
{
    //FromString parses a string into a TagEntry by its prefix
    public static TagEntry FromString(string s)
    {
        if (s.StartsWith("!#"))
        {
            return new TagEntry(Identifier.Parse(s[2..]), true, true);
        }
        if (s.StartsWith("#"))
        {
            return new TagEntry(Identifier.Parse(s[1..]), false, true);
        }
        if (s.StartsWith("!"))
        {
            return new TagEntry(Identifier.Parse(s[1..]), true, false);
        }
        return new TagEntry(Identifier.Parse(s), false, false);
    }

    //AsString serializes to the prefixed string form
    public string AsString()
    {
        var tagPrefix = IsTag ? "#" : "";
        var requiredPrefix = Required ? "!" : "";
        return requiredPrefix + tagPrefix + EntryId;
    }

    //Element factory that builds an element reference
    public static TagEntry Element(Identifier id, bool required) => new(id, required, false);

    //Tag factory that builds a tag reference
    public static TagEntry Tag(Identifier id, bool required) => new(id, required, true);

    //Build resolves the entry into elements, adds them to output and returns whether it succeeded
    //elementGetter resolves an element reference, returns false on failure when Required, otherwise ignores it
    //tagGetter resolves a tag reference and returns all of its elements
    public bool Build<T>(
        Func<Identifier, Optional<T>> elementGetter,
        Func<Identifier, Optional<IEnumerable<T>>> tagGetter,
        ICollection<T> output)
    {
        if (!IsTag)
        {
            var value = elementGetter(EntryId);
            if (value.IsPresent)
            {
                output.Add(value.Get());
                return true;
            }
            return !Required;
        }
        var tagValues = tagGetter(EntryId);
        if (tagValues.IsPresent)
        {
            foreach (var v in tagValues.Get())
            {
                output.Add(v);
            }
            return true;
        }
        return !Required;
    }

    public override string ToString() => AsString();
}
