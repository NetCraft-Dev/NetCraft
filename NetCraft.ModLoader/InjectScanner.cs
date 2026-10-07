using System.Reflection.Metadata;

namespace NetCraft.ModLoader;

//InjectScanner: reads Inject annotations from assembly metadata and produces rules with the same shape as the manifest
//Reads only the CustomAttribute table without loading assemblies or resolving attribute types
//This follows the same path as the manifest, so annotation rules and manifest rules are indistinguishable at the assembly stage
internal static class InjectScanner
{
    //InjectAttributeName: the type full name used to recognize the annotation
    //Compared by string rather than resolving the type; resolving would require NetCraft.ModApi to be present, which falls back to loading
    private const string InjectAttributeName = "NetCraft.ModApi.Extension.InjectAttribute";

    //MixinAttributeName: the type full name used to recognize the mixin annotation
    private const string MixinAttributeName = "NetCraft.ModApi.Extension.MixinAttribute";

    //SystemTypeName: the type name of System.Type
    //The System.Type reference in the metadata and GetSystemType must return the same value, otherwise the decoder cannot recognize typeof arguments
    private const string SystemTypeName = "System.Type";

    //Scan: scans all Inject annotations in this assembly and returns them in metadata order
    //A single entry that cannot be decoded is skipped without affecting the rest
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

    //ScanMixins: scans all Mixin annotations in this assembly, placed on the class
    //Unlike Inject, the source type is the annotated class itself and the target is read from the annotation
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

    //ReadMixin: decodes one mixin attribute, returns null when it is not a Mixin or has no target
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
            //Interfaces is Type[], decoded as a list of type serialized names
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

    //ReadRule: decodes one attribute, returns null when it is not Inject or lacks arguments
    //The replacement class and method are fixed as the class and method carrying this annotation
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
            //An undecipherable form appeared in the arguments, such as an enum; skip this entry
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

    //BoxedConstant: gets the raw value of a constant argument, unwrapping the decoded wrapper form
    //Whatever type it is after boxing is what the engine compares against
    private static object? BoxedConstant(object? value)
        => value is CustomAttributeTypedArgument<string> typed ? typed.Value : value;

    //ArgumentText: gets the value of a fixed argument
    //For a typeof argument what is baked into the metadata is the type serialized name, carried on Value, falling back to Type when absent
    private static string? ArgumentText(CustomAttributeTypedArgument<string> argument)
        => argument.Value as string ?? argument.Type;

    //AttributeTypeName: gets the full name of the type owning the attribute constructor
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

    //SerializedNameToFullName: reduces a type serialized name to the full name used in metadata
    //typeof emits a form like "FullName, AssemblyName, Version=..."
    //Generic arguments also contain commas, so only the one outside the brackets separates the name from the assembly
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

    //Normalize: nested types use a slash in metadata and a plus in serialized names
    private static string Normalize(string name) => name.Trim().Replace('+', '/');

    //TypeFullName: builds the full name of a type definition; nested types are assembled outward as a.b/c
    private static string TypeFullName(MetadataReader reader, TypeDefinition type)
    {
        var name = reader.GetString(type.Name);
        var declaring = type.GetDeclaringType();
        if (!declaring.IsNil)
            return TypeFullName(reader, reader.GetTypeDefinition(declaring)) + "/" + name;

        var ns = reader.GetString(type.Namespace);
        return ns.Length == 0 ? name : ns + "." + name;
    }

    //TypeRefName: builds the full name of a type reference
    private static string TypeRefName(MetadataReader reader, TypeReference reference)
    {
        var name = reader.GetString(reference.Name);
        var scope = reference.ResolutionScope;
        if (scope.Kind == HandleKind.TypeReference)
            return TypeRefName(reader, reader.GetTypeReference((TypeReferenceHandle)scope)) + "/" + name;

        var ns = reader.GetString(reference.Namespace);
        return ns.Length == 0 ? name : ns + "." + name;
    }

    //NameOnlyTypeProvider: a type provider that only works with names
    //During decoding it is asked for various types; everything round-trips as strings so nothing falls back to assembly loading
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
            => throw new BadImageFormatException($"injection annotations do not support enum arguments {type}");
    }
}
