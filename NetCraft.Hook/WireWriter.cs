using Mono.Cecil;
using Mono.Cecil.Cil;

namespace NetCraft.Hook;

//WireWriter 把 Cecil 的方法体编成原生注入层认的描述字节
//格式与 native\src\il\wire.rs 一一对应 两边改一处必须改另一处
internal static class WireWriter
{
    //操作数种类
    private const byte KindNone = 0;
    private const byte KindI32 = 1;
    private const byte KindI64 = 2;
    private const byte KindF32 = 3;
    private const byte KindF64 = 4;
    private const byte KindVar = 5;
    private const byte KindBranch = 6;
    private const byte KindSwitch = 7;
    private const byte KindRef = 9;
    private const byte KindField = 10;
    private const byte KindType = 11;
    private const byte KindString = 12;

    //元素类型标签
    private const byte ElementPtr = 0x0f;
    private const byte ElementByRef = 0x10;
    private const byte ElementValueType = 0x11;
    private const byte ElementClass = 0x12;
    private const byte ElementGenericInst = 0x15;
    private const byte ElementSZArray = 0x1d;

    //沿用原方法体的局部变量签名
    private const byte FlagCarryLocals = 0x01;

    //Write 编出一个方法的描述
    public static byte[] Write(MethodDefinition method)
    {
        var body = method.Body;
        var bytes = new List<byte>();

        //有局部变量就只能沿用原签名 这一版还不能给新方法体注入局部变量签名
        bytes.Add(body.Variables.Count > 0 ? FlagCarryLocals : (byte)0);
        AppendU16(bytes, (ushort)EstimateMaxStack(body));
        AppendU32(bytes, (uint)body.Instructions.Count);

        //分支目标用指令索引 不能用 Cecil 的 Offset 改写后它不一定更新
        var indexes = new Dictionary<Instruction, int>();
        for (var index = 0; index < body.Instructions.Count; index++)
            indexes[body.Instructions[index]] = index;

        //引用表在编指令时按出现次序收集 指令里只写索引
        var references = new List<MethodReference>();
        var fields = new List<FieldReference>();
        var types = new List<TypeReference>();
        var strings = new List<string>();
        foreach (var instruction in body.Instructions)
        {
            //Cecil 的 OpCode.Value 是 short 双字节指令为负数 要按位还原成 ushort
            AppendU16(bytes, unchecked((ushort)instruction.OpCode.Value));
            WriteOperand(bytes, method, instruction, indexes, references, fields, types, strings);
        }

        AppendU32(bytes, (uint)references.Count);
        foreach (var reference in references)
            WriteReference(bytes, reference);

        //字段表跟在方法引用表后面 原生侧按同一顺序读
        AppendU32(bytes, (uint)fields.Count);
        foreach (var field in fields)
            WriteFieldReference(bytes, field);

        //类型表跟在字段表后面 ldtoken 这类指令用它
        AppendU32(bytes, (uint)types.Count);
        foreach (var type in types)
            WriteTypeName(bytes, Definition(type));

        //字符串表在最后 内容是 UTF-16 字节 与元数据堆要的形状一致
        AppendU32(bytes, (uint)strings.Count);
        foreach (var text in strings)
            AppendWideText(bytes, text);

        return bytes.ToArray();
    }

    //EstimateMaxStack 给栈深一个宽松上界
    //精确算要处理分支汇合与按签名推栈效果的调用 不值得 原生侧还会再加余量
    private static int EstimateMaxStack(MethodBody body) =>
        Math.Max(body.MaxStackSize, body.Instructions.Count * 2 + 8);

    private static void WriteOperand(
        List<byte> bytes,
        MethodDefinition method,
        Instruction instruction,
        Dictionary<Instruction, int> indexes,
        List<MethodReference> references,
        List<FieldReference> fields,
        List<TypeReference> types,
        List<string> strings)
    {
        switch (instruction.Operand)
        {
            case null:
                bytes.Add(KindNone);
                break;
            case sbyte or byte or short or ushort or int or uint:
                bytes.Add(KindI32);
                AppendI32(bytes, Convert.ToInt32(instruction.Operand));
                break;
            case long or ulong:
                bytes.Add(KindI64);
                AppendI64(bytes, Convert.ToInt64(instruction.Operand));
                break;
            case float value:
                bytes.Add(KindF32);
                bytes.AddRange(BitConverter.GetBytes(value));
                break;
            case double value:
                bytes.Add(KindF64);
                bytes.AddRange(BitConverter.GetBytes(value));
                break;
            case VariableDefinition variable:
                bytes.Add(KindVar);
                AppendU16(bytes, (ushort)variable.Index);
                break;
            case ParameterDefinition parameter:
                bytes.Add(KindVar);
                AppendU16(bytes, (ushort)ArgumentSlot(method, parameter));
                break;
            case Instruction target:
                bytes.Add(KindBranch);
                AppendU32(bytes, (uint)indexes[target]);
                break;
            case Instruction[] targets:
                bytes.Add(KindSwitch);
                AppendU32(bytes, (uint)targets.Length);
                foreach (var item in targets)
                    AppendU32(bytes, (uint)indexes[item]);
                break;
            case MethodReference callee:
                bytes.Add(KindRef);
                AppendU32(bytes, (uint)references.Count);
                references.Add(callee);
                break;
            case FieldReference field:
                bytes.Add(KindField);
                AppendU32(bytes, (uint)fields.Count);
                fields.Add(field);
                break;
            //ldtoken 的操作数是类型本身 不是成员 MethodReference 与 FieldReference 都不覆盖这种情况
            case TypeReference type:
                bytes.Add(KindType);
                AppendU32(bytes, (uint)types.Count);
                types.Add(type);
                break;
            //ldstr 的操作数是字符串字面量 它在元数据里有自己的堆
            case string text:
                bytes.Add(KindString);
                AppendU32(bytes, (uint)strings.Count);
                strings.Add(text);
                break;
            default:
                throw new NotSupportedException(
                    $"Runtime injection does not support this operand type {instruction.Operand.GetType().Name} in {method.FullName}");
        }
    }

    //ArgumentSlot 参数在 IL 里的槽位 实例方法的 this 占 0 号
    private static int ArgumentSlot(MethodDefinition method, ParameterDefinition parameter) =>
        method.HasThis ? parameter.Index + 1 : parameter.Index;

    private static void WriteReference(List<byte> bytes, MethodReference callee)
    {
        WriteTypeName(bytes, Definition(callee.DeclaringType));
        AppendText(bytes, callee.Name);
        bytes.Add(callee.HasThis ? (byte)1 : (byte)0);
        bytes.Add((byte)callee.Parameters.Count);
        foreach (var parameter in callee.Parameters)
            WriteType(bytes, parameter.ParameterType);
        WriteType(bytes, callee.ReturnType);
    }

    //WriteFieldReference 编一条字段引用 没有参数表也没有 HASTHIS 一个字段类型就够
    private static void WriteFieldReference(List<byte> bytes, FieldReference field)
    {
        WriteTypeName(bytes, Definition(field.DeclaringType));
        AppendText(bytes, field.Name);
        WriteType(bytes, field.FieldType);
    }

    //Definition 泛型实例化退回到泛型定义
    //成员引用的父类型指向的是定义本身 实例化信息在签名里表达 两者不能混
    private static TypeReference Definition(TypeReference type) =>
        type is GenericInstanceType generic ? generic.ElementType : type;

    //WriteTypeName 写一个类型定义的引用 程序集与全名
    private static void WriteTypeName(List<byte> bytes, TypeReference definition)
    {
        AppendText(bytes, ScopeName(definition));
        AppendText(bytes, definition.FullName);
    }

    private static void WriteType(List<byte> bytes, TypeReference type)
    {
        var primitive = PrimitiveTag(type);
        if (primitive != 0)
        {
            bytes.Add(primitive);
            return;
        }

        switch (type)
        {
            //只认一维零基数组 多维要带界信息 暂不支持
            case ArrayType { Rank: 1 } array:
                bytes.Add(ElementSZArray);
                WriteType(bytes, array.ElementType);
                return;
            case ByReferenceType byReference:
                bytes.Add(ElementByRef);
                WriteType(bytes, byReference.ElementType);
                return;
            case PointerType pointer:
                bytes.Add(ElementPtr);
                WriteType(bytes, pointer.ElementType);
                return;
            //泛型实例化 先写泛型定义再写各个实参 与 ECMA 335 的 GENERICINST 同一形状
            case GenericInstanceType generic:
                bytes.Add(ElementGenericInst);
                bytes.Add(generic.ElementType.IsValueType ? ElementValueType : ElementClass);
                WriteTypeName(bytes, generic.ElementType);
                bytes.Add((byte)generic.GenericArguments.Count);
                foreach (var argument in generic.GenericArguments)
                    WriteType(bytes, argument);
                return;
        }

        bytes.Add(type.IsValueType ? ElementValueType : ElementClass);
        WriteTypeName(bytes, type);
    }

    //PrimitiveTag 基本类型直接给元素类型字节 自定义类型返回 0
    private static byte PrimitiveTag(TypeReference type) => type.MetadataType switch
    {
        MetadataType.Void => 0x01,
        MetadataType.Boolean => 0x02,
        MetadataType.Char => 0x03,
        MetadataType.SByte => 0x04,
        MetadataType.Byte => 0x05,
        MetadataType.Int16 => 0x06,
        MetadataType.UInt16 => 0x07,
        MetadataType.Int32 => 0x08,
        MetadataType.UInt32 => 0x09,
        MetadataType.Int64 => 0x0a,
        MetadataType.UInt64 => 0x0b,
        MetadataType.Single => 0x0c,
        MetadataType.Double => 0x0d,
        MetadataType.String => 0x0e,
        MetadataType.IntPtr => 0x18,
        MetadataType.UIntPtr => 0x19,
        MetadataType.Object => 0x1c,
        _ => 0,
    };

    //ScopeName 取类型或方法所在程序集的简单名
    //同模块的定义拿到的是 ModuleDefinition 要退回去取所属程序集名
    private static string ScopeName(TypeReference type) => type.Scope switch
    {
        AssemblyNameReference assembly => assembly.Name,
        ModuleDefinition module => module.Assembly.Name.Name,
        _ => type.Module.Assembly.Name.Name,
    };

    private static void AppendU16(List<byte> bytes, ushort value) =>
        bytes.AddRange(BitConverter.GetBytes(value));

    private static void AppendU32(List<byte> bytes, uint value) =>
        bytes.AddRange(BitConverter.GetBytes(value));

    private static void AppendI32(List<byte> bytes, int value) =>
        bytes.AddRange(BitConverter.GetBytes(value));

    private static void AppendI64(List<byte> bytes, long value) =>
        bytes.AddRange(BitConverter.GetBytes(value));

    private static void AppendText(List<byte> bytes, string text)
    {
        var raw = System.Text.Encoding.UTF8.GetBytes(text);
        AppendU16(bytes, (ushort)raw.Length);
        bytes.AddRange(raw);
    }

    //AppendWideText 写一段 UTF-16 文本 长度按字节算
    //字符串字面量走这条 元数据堆存的就是 UTF-16 原生侧不必再转一遍
    private static void AppendWideText(List<byte> bytes, string text)
    {
        var raw = System.Text.Encoding.Unicode.GetBytes(text);
        AppendU16(bytes, (ushort)raw.Length);
        bytes.AddRange(raw);
    }
}
