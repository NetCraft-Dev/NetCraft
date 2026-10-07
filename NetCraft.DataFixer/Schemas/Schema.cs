namespace NetCraft.DataFixer.Schemas;

using System;
using System.Collections.Generic;
using System.Linq;
using NetCraft.DataFixer;
using T = NetCraft.DataFixer.Types;
using NetCraft.DataFixer.Types.Families;
using NetCraft.DataFixer.Types.Templates;

//Schema type architecture maps to vanilla com.mojang.datafixers.schemas.Schema
//manages all TypeTemplates of a version and builds the RecursiveTypeFamily
public class Schema
{
    private readonly Dictionary<string, int> _recursiveTypes = new();
    private readonly Dictionary<string, Func<TypeTemplate>> _typeTemplates = new();
    private readonly Dictionary<string, T.Type<object>> _types;
    private readonly int _versionKey;
    private readonly string _name;
    private readonly Schema? _parent;

    public Schema(int versionKey, Schema? parent)
    {
        _versionKey = versionKey;
        int subVersion = DataFixUtils.GetSubVersion(versionKey);
        _name = "V" + DataFixUtils.GetVersion(versionKey) + (subVersion == 0 ? "" : "." + subVersion);
        _parent = parent;
        RegisterTypes(this, RegisterEntities(this), RegisterBlockEntities(this));
        _types = BuildTypes();
    }

    //buildTypes builds the Type for every TypeTemplate; recursive types are Check-wrapped then folded into choice
    protected Dictionary<string, T.Type<object>> BuildTypes()
    {
        var types = new Dictionary<string, T.Type<object>>();
        var templates = new List<TypeTemplate>();
        foreach (var kv in _recursiveTypes)
        {
            templates.Add(DSL.Check(kv.Key, kv.Value, GetTemplate(kv.Key)));
        }
        TypeTemplate choice = templates[0];
        for (int i = 1; i < templates.Count; i++)
        {
            choice = DSL.Or(choice, templates[i]);
        }
        TypeFamily family = new RecursiveTypeFamily(_name, choice);
        foreach (var name in _typeTemplates.Keys)
        {
            T.Type<object> type;
            if (_recursiveTypes.TryGetValue(name, out var recurseId))
            {
                type = family.Apply(recurseId);
            }
            else
            {
                type = GetTemplate(name).Apply(family).Apply(-1);
            }
            types[name] = type;
        }
        return types;
    }

    public HashSet<string> Types() => new(_types.Keys);

    //getTypeRaw takes the raw type by reference; throws for unknown types
    public T.Type<object> GetTypeRaw(DSL.ITypeReference type)
    {
        var name = type.TypeName();
        if (_types.TryGetValue(name, out var t)) return t;
        throw new ArgumentException("Unknown type: " + name);
    }

    //getType takes the type by reference; recursive points expand checked
    public T.Type<object> GetType(DSL.ITypeReference type)
    {
        var name = type.TypeName();
        if (!_types.TryGetValue(name, out var type1))
        {
            throw new ArgumentException("Unknown type: " + name);
        }
        if (type1 is RecursivePoint.RecursivePointType<object> recursivePoint)
        {
            var checkedOpt = recursivePoint.FindCheckedType(-1);
            if (checkedOpt.IsPresent) return checkedOpt.Get();
            throw new InvalidOperationException("Could not find choice type in the recursive type");
        }
        return type1!;
    }

    //resolveTemplate resolves a template by name; throws when unknown
    public TypeTemplate ResolveTemplate(string name)
    {
        if (_typeTemplates.TryGetValue(name, out var supplier)) return supplier();
        throw new ArgumentException("Unknown type: " + name);
    }

    //id returns a recursive point template or a plain template by name
    public TypeTemplate Id(string name)
    {
        if (_recursiveTypes.TryGetValue(name, out var id)) return DSL.Id(id);
        return GetTemplate(name);
    }

    //getTemplate constructs a Named template by name
    protected TypeTemplate GetTemplate(string name)
        => DSL.Named(name, ResolveTemplate(name));

    //getChoiceType takes the corresponding child type by reference and choice name
    public virtual T.Type<object> GetChoiceType(DSL.ITypeReference type, string choiceName)
    {
        var choiceType = FindChoiceType(type);
        var types = choiceType.Types();
        if (!types.ContainsKey(choiceName))
        {
            throw new ArgumentException("Data fixer not registered for: " + choiceName + " in " + type.TypeName());
        }
        return types[choiceName];
    }

    //findChoiceType takes a TaggedChoiceType by reference
    //the actual type is TaggedChoiceType<K> where K may be string or object
    //under C# strict generic invariance TaggedChoiceType<string> cannot be cast to TaggedChoiceType<object>
    //use Unsafe.As to bypass the runtime type check, aligning with Java type erasure semantics
    public TaggedChoice<object>.TaggedChoiceType<object> FindChoiceType(DSL.ITypeReference type)
    {
        var opt = GetType(type).FindChoiceType("id", -1);
        if (!opt.IsPresent) throw new ArgumentException("Not a choice type");
        var obj = opt.Get();
        var result = System.Runtime.CompilerServices.Unsafe.As<object, TaggedChoice<object>.TaggedChoiceType<object>>(ref obj);
        return result;
    }

    public virtual void RegisterTypes(Schema schema, Dictionary<string, Func<TypeTemplate>> entityTypes, Dictionary<string, Func<TypeTemplate>> blockEntityTypes)
        => _parent?.RegisterTypes(schema, entityTypes, blockEntityTypes);

    public virtual Dictionary<string, Func<TypeTemplate>> RegisterEntities(Schema schema)
        => _parent?.RegisterEntities(schema) ?? new();

    public virtual Dictionary<string, Func<TypeTemplate>> RegisterBlockEntities(Schema schema)
        => _parent?.RegisterBlockEntities(schema) ?? new();

    //registerSimple registers with a remainder template
    public void RegisterSimple(Dictionary<string, Func<TypeTemplate>> map, string name)
        => Register(map, name, _ => DSL.Remainder());

    //register registers by name and template factory
    public void Register(Dictionary<string, Func<TypeTemplate>> map, string name, Func<string, TypeTemplate> template)
        => Register(map, name, () => template(name));

    public void Register(Dictionary<string, Func<TypeTemplate>> map, string name, Func<TypeTemplate> template)
        => map[name] = template;

    //registerType registers a type template; recursive decides whether it joins the recursive family
    public void RegisterType(bool recursive, DSL.ITypeReference type, Func<TypeTemplate> template)
    {
        _typeTemplates[type.TypeName()] = template;
        if (recursive && !_recursiveTypes.ContainsKey(type.TypeName()))
        {
            _recursiveTypes[type.TypeName()] = _recursiveTypes.Count;
        }
    }

    public int GetVersionKey() => _versionKey;
    public Schema? GetParent() => _parent;
}
