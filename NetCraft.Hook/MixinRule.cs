namespace NetCraft.Hook;

//TypeRef 按名字指一个类型 引用从元数据拼出来 不把它加载起来
//混入与接口注入都发生在内核程序集进内存之前 那个时刻用反射解析类型会把未改写的程序集提前拉进来
public sealed class TypeRef
{
    public TypeRef(string typeName, string? assemblyName = null)
    {
        TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
        AssemblyName = assemblyName;
    }

    //TypeName 类型全名 嵌套类型用斜杠分隔
    public string TypeName { get; }

    //AssemblyName 类型所在程序集简单名 留空表示在当前模块里找
    public string? AssemblyName { get; }

    public override string ToString() => AssemblyName is null ? TypeName : $"{TypeName} ({AssemblyName})";
}

//MixinRule 把来源类型的成员搬进目标类型
//搬是搬不是复制 来源类型里这些成员会被移走 搬完只剩空壳 模组代码不该再用它
//只对加载期改写生效 运行时注入只提交方法体 既加不了成员也改不了类型布局
public sealed class MixinRule
{
    public MixinRule(string targetType, string sourceAssembly, string sourceTypeName, string? description = null)
        : this(targetType, sourceAssembly, sourceTypeName, null, description)
    {
    }

    public MixinRule(string targetType, string sourceAssembly, string sourceTypeName,
        IEnumerable<TypeRef>? interfaces, string? description = null)
    {
        TargetType = targetType ?? throw new ArgumentNullException(nameof(targetType));
        SourceAssembly = sourceAssembly ?? throw new ArgumentNullException(nameof(sourceAssembly));
        SourceTypeName = sourceTypeName ?? throw new ArgumentNullException(nameof(sourceTypeName));
        Interfaces = interfaces?.ToList() ?? new List<TypeRef>();
        Description = description;
    }

    //TargetType 目标类型全名 成员搬到这里
    public string TargetType { get; }

    //SourceAssembly 来源类型所在程序集简单名
    public string SourceAssembly { get; }

    //SourceTypeName 来源类型全名 它的字段与方法会被搬走
    public string SourceTypeName { get; }

    //Interfaces 顺带加进目标类型接口表的接口
    public IReadOnlyList<TypeRef> Interfaces { get; }

    //Description 说明
    public string? Description { get; }

    public override string ToString()
    {
        var interfaces = Interfaces.Count == 0 ? "" : $" +{Interfaces.Count} itf";
        var note = Description is null ? "" : $" ({Description})";
        return $"[Mixin] {TargetType} ← {SourceAssembly}!{SourceTypeName}{interfaces}{note}";
    }
}
