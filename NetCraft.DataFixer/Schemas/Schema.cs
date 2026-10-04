namespace NetCraft.DataFixer.Schemas;

using System;
using System.Collections.Generic;
using System.Linq;
using NetCraft.DataFixer;
using T = NetCraft.DataFixer.Types;
using NetCraft.DataFixer.Types.Families;
using NetCraft.DataFixer.Types.Templates;

//Schema类型架构对应原版com.mojang.datafixers.schemas.Schema
//管理一个版本的所有TypeTemplate并构建RecursiveTypeFamily
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

    //buildTypes构建所有TypeTemplate对应的Type递归类型用Check包装后折叠为choice
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

    //getTypeRaw按引用取得原始类型未知类型抛异常
    public T.Type<object> GetTypeRaw(DSL.ITypeReference type)
    {
        var name = type.TypeName();
        if (_types.TryGetValue(name, out var t)) return t;
        throw new ArgumentException("Unknown type: " + name);
    }

    //getType按引用取得类型递归点会展开checked
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

    //resolveTemplate按名解析模板未知抛异常
    public TypeTemplate ResolveTemplate(string name)
    {
        if (_typeTemplates.TryGetValue(name, out var supplier)) return supplier();
        throw new ArgumentException("Unknown type: " + name);
    }

    //id按名返回递归点模板或普通模板
    public TypeTemplate Id(string name)
    {
        if (_recursiveTypes.TryGetValue(name, out var id)) return DSL.Id(id);
        return GetTemplate(name);
    }

    //getTemplate按名构造Named模板
    protected TypeTemplate GetTemplate(string name)
        => DSL.Named(name, ResolveTemplate(name));

    //getChoiceType按引用与choice名取得对应子类型
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

    //findChoiceType按引用取得TaggedChoiceType
    //实际类型是TaggedChoiceType<K>其中K可能是string或object
    //C#严格泛型不变量下TaggedChoiceType<string>不能cast为TaggedChoiceType<object>
    //用Unsafe.As绕过运行时类型检查对齐Java类型擦除语义
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

    //registerSimple按remainder模板注册
    public void RegisterSimple(Dictionary<string, Func<TypeTemplate>> map, string name)
        => Register(map, name, _ => DSL.Remainder());

    //register按名与模板工厂注册
    public void Register(Dictionary<string, Func<TypeTemplate>> map, string name, Func<string, TypeTemplate> template)
        => Register(map, name, () => template(name));

    public void Register(Dictionary<string, Func<TypeTemplate>> map, string name, Func<TypeTemplate> template)
        => map[name] = template;

    //registerType注册类型模板recursive决定是否参与递归家族
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
