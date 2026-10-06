using System.Reflection.Metadata;

namespace NetCraft.ModLoader;

//InjectScanner 从程序集元数据里读 Inject 注解 产出与清单同形的规则
//全程只读 CustomAttribute 表 不加载程序集也不解析特性类型
//这点与清单走的是同一条路 所以注解规则和清单规则在装配阶段没有任何区别
internal static class InjectScanner
{
    //InjectAttributeName 识别注解用的类型全名
    //按字符串比对而不是解析类型 一解析就要求 NetCraft.ModApi 在场 那就退回加载了
    private const string InjectAttributeName = "NetCraft.ModApi.Extension.InjectAttribute";

    //MixinAttributeName 识别混入注解用的类型全名
    private const string MixinAttributeName = "NetCraft.ModApi.Extension.MixinAttribute";

    //SystemTypeName System.Type 的类型名
    //元数据里的 System.Type 引用与 GetSystemType 必须给同一个值 否则解码器认不出 typeof 参数
    private const string SystemTypeName = "System.Type";

    //Scan 扫出该程序集里全部 Inject 注解 按元数据顺序返回
    //单条解不开只跳过它 不影响其余规则
    public static List<ModHookRule> Scan(MetadataReader reader)
    {
        var rules = new List<ModHookRule>();
        var provider = new NameOnlyTypeProvider();

        foreach (var typeHandle in reader.TypeDefinitions)
        {
            var type = reader.GetTypeDefinition(typeHandle);
            var replaceType = TypeFullName(reader, type);
            foreach (var methodHandle in type.GetMethods())
            {
                var method = reader.GetMethodDefinition(methodHandle);
                foreach (var attributeHandle in method.GetCustomAttributes())
                {
                    var rule = ReadRule(reader, attributeHandle, provider, replaceType, reader.GetString(method.Name));
                    if (rule is not null)
                        rules.Add(rule);
                }
            }
        }
        return rules;
    }

    //ScanMixins 扫出该程序集里全部 Mixin 注解 标在类上
    //与 Inject 相反 来源类型就是被标注的那个类 从注解里读的是目标
    public static List<ModMixinRule> ScanMixins(MetadataReader reader)
    {
        var rules = new List<ModMixinRule>();
        var provider = new NameOnlyTypeProvider();

        foreach (var typeHandle in reader.TypeDefinitions)
        {
            var type = reader.GetTypeDefinition(typeHandle);
            var source = TypeFullName(reader, type);
            foreach (var attributeHandle in type.GetCustomAttributes())
            {
                var rule = ReadMixin(reader, attributeHandle, provider, source);
                if (rule is not null)
                    rules.Add(rule);
            }
        }
        return rules;
    }

    //ReadMixin 解一条混入特性 不是 Mixin 或没写目标时返回 null
    private static ModMixinRule? ReadMixin(
        MetadataReader reader,
        CustomAttributeHandle handle,
        NameOnlyTypeProvider provider,
        string sourceType)
    {
        var attribute = reader.GetCustomAttribute(handle);
        if (AttributeTypeName(reader, attribute) != MixinAttributeName)
            return null;

        CustomAttributeValue<string> value;
        try
        {
            value = attribute.DecodeValue(provider);
        }
        catch (Exception)
        {
            return null;
        }

        if (value.FixedArguments.Length < 1)
            return null;

        var target = ArgumentText(value.FixedArguments[0]);
        if (string.IsNullOrEmpty(target))
            return null;

        var rule = new ModMixinRule { Target = target, Source = sourceType };

        foreach (var named in value.NamedArguments)
        {
            //Interfaces 是 Type[] 解出来是一串类型序列化名
            if (named.Name != "Interfaces" || named.Value is not System.Collections.IEnumerable items)
                continue;

            foreach (var item in items)
            {
                if (item is not CustomAttributeTypedArgument<string> argument)
                    continue;
                var name = ArgumentText(argument);
                if (!string.IsNullOrEmpty(name))
                    rule.Interfaces.Add(name);
            }
        }
        return rule;
    }

    //ReadRule 解一条特性 不是 Inject 或参数不全时返回 null
    //替换类与替换方法固定是标注这个方法的类与方法
    private static ModHookRule? ReadRule(
        MetadataReader reader,
        CustomAttributeHandle handle,
        NameOnlyTypeProvider provider,
        string replaceType,
        string replaceMethod)
    {
        var attribute = reader.GetCustomAttribute(handle);
        if (AttributeTypeName(reader, attribute) != InjectAttributeName)
            return null;

        CustomAttributeValue<string> value;
        try
        {
            value = attribute.DecodeValue(provider);
        }
        catch (Exception)
        {
            //参数里出现了解不了的形态 例如枚举 跳过这一条
            return null;
        }

        if (value.FixedArguments.Length < 2)
            return null;

        var target = ArgumentText(value.FixedArguments[0]);
        var method = value.FixedArguments[1].Value as string;
        if (string.IsNullOrEmpty(target) || string.IsNullOrEmpty(method))
            return null;

        var rule = new ModHookRule
        {
            Target = target,
            Method = method,
            ReplaceType = replaceType,
            ReplaceMethod = replaceMethod,
        };

        foreach (var named in value.NamedArguments)
        {
            switch (named.Name)
            {
                case "HookType" when named.Value is string hookType && !string.IsNullOrWhiteSpace(hookType):
                    rule.HookTypeName = hookType;
                    break;
                case "PatchMode" when named.Value is string patchMode && !string.IsNullOrWhiteSpace(patchMode):
                    rule.PatchModeName = patchMode;
                    break;
                case "Label":
                    rule.Label = named.Value as string;
                    break;
                case "Ordinal" when named.Value is int ordinal:
                    rule.Ordinal = ordinal;
                    break;
                case "ArgumentIndex" when named.Value is int argumentIndex:
                    rule.ArgumentIndex = argumentIndex;
                    break;
                case "SliceFrom":
                    rule.SliceFrom = named.Value as string;
                    break;
                case "SliceTo":
                    rule.SliceTo = named.Value as string;
                    break;
                case "InType":
                    rule.InType = named.Value as string;
                    break;
                case "InMethod":
                    rule.InMethod = named.Value as string;
                    break;
                case "Placement" when named.Value is string placement && !string.IsNullOrWhiteSpace(placement):
                    rule.PlacementName = placement;
                    break;
                case "LocalIndex" when named.Value is int localIndex:
                    rule.LocalIndex = localIndex;
                    break;
                case "ConstantValue":
                    rule.ScannedConstantValue = BoxedConstant(named.Value);
                    break;
                case "Environment" when named.Value is string environment && !string.IsNullOrWhiteSpace(environment):
                    rule.EnvironmentValue = environment;
                    break;
            }
        }
        return rule;
    }

    //BoxedConstant 取常量参数的裸值 解成包装形态的再剥一层
    //装箱后是什么类型就是什么类型 引擎比对时按那个类型走
    private static object? BoxedConstant(object? value)
        => value is CustomAttributeTypedArgument<string> typed ? typed.Value : value;

    //ArgumentText 取一个固定参数的值
    //typeof 参数编进元数据的是类型序列化名 落在 Value 上 退化时再看 Type
    private static string? ArgumentText(CustomAttributeTypedArgument<string> argument)
        => argument.Value as string ?? argument.Type;

    //AttributeTypeName 取特性构造器所属类型的全名
    private static string? AttributeTypeName(MetadataReader reader, CustomAttribute attribute)
    {
        switch (attribute.Constructor.Kind)
        {
            case HandleKind.MethodDefinition:
                var method = reader.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor);
                return TypeFullName(reader, reader.GetTypeDefinition(method.GetDeclaringType()));

            case HandleKind.MemberReference:
                var member = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
                return member.Parent.Kind switch
                {
                    HandleKind.TypeReference => TypeRefName(reader, reader.GetTypeReference((TypeReferenceHandle)member.Parent)),
                    HandleKind.TypeDefinition => TypeFullName(reader, reader.GetTypeDefinition((TypeDefinitionHandle)member.Parent)),
                    _ => null,
                };

            default:
                return null;
        }
    }

    //SerializedNameToFullName 把类型序列化名收敛成元数据里的全名
    //typeof 编出来的是 "全名, 程序集名, Version=..." 这种形态
    //泛型实参里也有逗号 所以只有方括号外的那个才是名字与程序集的分隔
    private static string SerializedNameToFullName(string serialized)
    {
        var depth = 0;
        for (var i = 0; i < serialized.Length; i++)
        {
            var ch = serialized[i];
            if (ch == '[') depth++;
            else if (ch == ']') depth--;
            else if (ch == ',' && depth == 0)
                return Normalize(serialized[..i]);
        }
        return Normalize(serialized);
    }

    //Normalize 嵌套类型元数据里用斜杠 序列化名里用加号
    private static string Normalize(string name) => name.Trim().Replace('+', '/');

    //TypeFullName 拼类型定义的全名 嵌套类型逐层向外拼成 a.b/c 形式
    private static string TypeFullName(MetadataReader reader, TypeDefinition type)
    {
        var name = reader.GetString(type.Name);
        var declaring = type.GetDeclaringType();
        if (!declaring.IsNil)
            return TypeFullName(reader, reader.GetTypeDefinition(declaring)) + "/" + name;

        var ns = reader.GetString(type.Namespace);
        return ns.Length == 0 ? name : ns + "." + name;
    }

    //TypeRefName 拼类型引用的全名
    private static string TypeRefName(MetadataReader reader, TypeReference reference)
    {
        var name = reader.GetString(reference.Name);
        var scope = reference.ResolutionScope;
        if (scope.Kind == HandleKind.TypeReference)
            return TypeRefName(reader, reader.GetTypeReference((TypeReferenceHandle)scope)) + "/" + name;

        var ns = reader.GetString(reference.Namespace);
        return ns.Length == 0 ? name : ns + "." + name;
    }

    //NameOnlyTypeProvider 只认名字的类型提供者
    //解码过程中它会被问各种类型 全部按字符串往返 一个都不落到程序集加载上
    private sealed class NameOnlyTypeProvider : ICustomAttributeTypeProvider<string>
    {
        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode switch
        {
            PrimitiveTypeCode.String => "System.String",
            PrimitiveTypeCode.Int32 => "System.Int32",
            PrimitiveTypeCode.Boolean => "System.Boolean",
            _ => typeCode.ToString(),
        };

        public string GetSystemType() => SystemTypeName;

        public bool IsSystemType(string type) => type == SystemTypeName;

        public string GetSZArrayType(string elementType) => elementType + "[]";

        public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
            => TypeFullName(reader, reader.GetTypeDefinition(handle));

        public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
            => TypeRefName(reader, reader.GetTypeReference(handle));

        public string GetTypeFromSerializedName(string name) => SerializedNameToFullName(name);

        public PrimitiveTypeCode GetUnderlyingEnumType(string type)
            => throw new BadImageFormatException($"注入注解不支持枚举参数 {type}");
    }
}
