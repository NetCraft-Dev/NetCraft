using Mono.Cecil;
using Mono.Cecil.Cil;

namespace NetCraft.Hook;

//HookEngine 的混入部分
//把来源类型的成员搬进目标类型 与指令级注入共用同一趟改写 搬完的方法还会参与后面的规则匹配
public sealed partial class HookEngine
{
    //_mixins 已登记的混入规则
    private readonly List<MixinRule> _mixins = new();

    //_mixinSources 来源程序集的按需读取器 键是程序集简单名
    //改写目标那一刻来源程序集通常还没加载 只能读它的字节用元数据解析
    private readonly Dictionary<string, Func<byte[]?>> _mixinSources = new();

    //_resolvedMixinSources 读过的来源定义 同一个来源被多条规则用到时只解析一次
    private readonly Dictionary<string, AssemblyDefinition?> _resolvedMixinSources = new(StringComparer.Ordinal);

    //Mixins 已登记的混入规则
    public IReadOnlyList<MixinRule> Mixins => _mixins.AsReadOnly();

    //AddMixin 登记一条混入规则
    public HookEngine AddMixin(MixinRule rule)
    {
        EnsureNotSealed();
        _mixins.Add(rule ?? throw new ArgumentNullException(nameof(rule)));
        return this;
    }

    //AddMixins 批量登记混入规则
    public HookEngine AddMixins(IEnumerable<MixinRule> rules)
    {
        EnsureNotSealed();
        foreach (var rule in rules)
            _mixins.Add(rule);
        return this;
    }

    //RegisterMixinSource 登记来源程序集的字节读取器
    //改写期间才需要它 那时按名字把字节取来交给 Cecil 解析
    public HookEngine RegisterMixinSource(string assemblyName, Func<byte[]?> loader)
    {
        EnsureNotSealed();
        _mixinSources[assemblyName] = loader ?? throw new ArgumentNullException(nameof(loader));
        return this;
    }

    //ApplyMixins 把命中的混入规则应用到程序集里
    //放在指令级改写之前 搬进来的方法照样会被后面的规则扫到
    private void ApplyMixins(AssemblyDefinition asm, ModuleDefinition module)
    {
        if (_mixins.Count == 0)
            return;

        //来源定义只在一趟改写里复用
        //搬成员会把来源对象本身改掉 跨趟沿用第二趟就什么都不剩了
        _resolvedMixinSources.Clear();

        //成员搬动会改类型的成员表 先取快照再遍历
        foreach (var type in module.GetTypes().ToList())
        {
            foreach (var rule in _mixins)
            {
                if (rule.TargetType != type.FullName)
                    continue;
                ApplyMixin(type, rule, module);
            }
        }
    }

    //ApplyMixin 把一条规则落到一个目标类型上
    private void ApplyMixin(TypeDefinition target, MixinRule rule, ModuleDefinition module)
    {
        var source = ResolveMixinSource(rule);
        //来源拿不到或指到目标自己都不做 静默跳过 装配期该由调用方把这两类问题查出来
        if (source is null || source.FullName == target.FullName)
            return;

        MoveFields(target, source, module);
        MoveProperties(target, source, module);
        MoveEvents(target, source, module);
        var moved = MoveMethods(target, source, module);

        //带接口的规则要把搬过来的实例方法标成虚方法
        //接口分派只认虚表 一个普通方法搬过去 CLR 会判定接口没被实现 直接 TypeLoadException
        //NewSlot 是必须的 不标就变成覆盖基类同名方法
        if (rule.Interfaces.Count > 0)
        {
            foreach (var method in moved)
            {
                if (method.IsStatic || !method.IsPublic || method.IsVirtual)
                    continue;
                method.IsVirtual = true;
                method.IsNewSlot = true;
            }
        }

        //成员都搬完之后才能在目标类型上按名字找回定义 所以重定向与构造器合并排在后面
        MergeConstructors(target, source, module);
        foreach (var method in moved)
            RedirectBody(method, source, target, module);

        AddInterfaces(target, rule, module);
        _rewriteCount++;
    }

    //ResolveMixinSource 读来源程序集的定义 取不到返回 null
    private TypeDefinition? ResolveMixinSource(MixinRule rule)
    {
        if (!_resolvedMixinSources.TryGetValue(rule.SourceAssembly, out var asm))
        {
            asm = null;
            if (_mixinSources.TryGetValue(rule.SourceAssembly, out var loader))
            {
                var bytes = loader();
                if (bytes is { Length: > 0 })
                {
                    //不能用 using 包住 后面搬成员时还要读它的定义
                    var parameters = new ReaderParameters
                    {
                        ReadingMode = ReadingMode.Immediate,
                        ReadWrite = false,
                        InMemory = true,
                    };
                    asm = AssemblyDefinition.ReadAssembly(new MemoryStream(bytes), parameters);
                }
            }
            _resolvedMixinSources[rule.SourceAssembly] = asm;
        }

        return asm?.MainModule.GetType(rule.SourceTypeName);
    }

    //MoveFields 搬字段 目标上已有同名的留着不动
    //签名里的类型引用要跟着导进目标模块 写元数据时 Cecil 不会自己跨模块解析
    private static void MoveFields(TypeDefinition target, TypeDefinition source, ModuleDefinition module)
    {
        foreach (var field in source.Fields.ToList())
        {
            if (target.Fields.Any(f => f.Name == field.Name))
                continue;
            source.Fields.Remove(field);
            field.DeclaringType = target;
            field.FieldType = module.ImportReference(field.FieldType);
            target.Fields.Add(field);
        }
    }

    //MoveProperties 搬属性定义 accessor 已经被方法搬运带走 定义留在来源类型上就悬空了
    private static void MoveProperties(TypeDefinition target, TypeDefinition source, ModuleDefinition module)
    {
        foreach (var property in source.Properties.ToList())
        {
            if (target.Properties.Any(p => p.Name == property.Name))
                continue;
            source.Properties.Remove(property);
            property.DeclaringType = target;
            property.PropertyType = module.ImportReference(property.PropertyType);
            target.Properties.Add(property);
        }
    }

    //MoveEvents 搬事件定义 理由同属性
    private static void MoveEvents(TypeDefinition target, TypeDefinition source, ModuleDefinition module)
    {
        foreach (var @event in source.Events.ToList())
        {
            if (target.Events.Any(e => e.Name == @event.Name))
                continue;
            source.Events.Remove(@event);
            @event.DeclaringType = target;
            @event.EventType = module.ImportReference(@event.EventType);
            target.Events.Add(@event);
        }
    }

    //MoveMethods 搬方法 构造器不走这里 它们由 MergeConstructors 并进目标的构造器
    //返回搬走的方法 引用重定向要拿这份清单
    private static List<MethodDefinition> MoveMethods(TypeDefinition target, TypeDefinition source, ModuleDefinition module)
    {
        var moved = new List<MethodDefinition>();
        foreach (var method in source.Methods.ToList())
        {
            if (method.IsConstructor)
                continue;
            if (target.Methods.Any(m => m.Name == method.Name && m.Parameters.Count == method.Parameters.Count))
                continue;

            //方法体是延迟读的 定位靠方法所属的模块 而 Module 又跟着 DeclaringType 走
            //搬走之后再去读就会拿目标模块去查来源里的 RVA 先在这里逼它读出来并缓存
            _ = method.Body;

            source.Methods.Remove(method);
            method.DeclaringType = target;
            method.ReturnType = module.ImportReference(method.ReturnType);
            foreach (var parameter in method.Parameters)
                parameter.ParameterType = module.ImportReference(parameter.ParameterType);
            target.Methods.Add(method);
            moved.Add(method);
        }
        return moved;
    }

    //RedirectBody 把搬过来的方法里对来源类型成员的引用改指目标类型 再把其余引用导进目标模块
    //只动字段与方法引用 操作数是类型本身的指令（isinst / newobj / ldtoken）保持原样
    private static void RedirectBody(MethodDefinition method, TypeDefinition from, TypeDefinition to, ModuleDefinition module)
    {
        if (method.Body is null)
            return;

        foreach (var instruction in method.Body.Instructions)
        {
            switch (instruction.Operand)
            {
                case FieldReference field when field.DeclaringType?.FullName == from.FullName:
                    instruction.Operand = FindField(to, field) ?? field;
                    break;
                case MethodReference called when called.DeclaringType?.FullName == from.FullName:
                    instruction.Operand = FindMethod(to, called) ?? called;
                    break;
            }

            //换定义与导模块要分开做 先换成本模块里的定义 剩下的外部引用再整体导一遍
            ImportOperand(module, instruction);
        }

        foreach (var variable in method.Body.Variables)
            variable.VariableType = module.ImportReference(variable.VariableType);

        foreach (var handler in method.Body.ExceptionHandlers)
        {
            if (handler.CatchType is not null)
                handler.CatchType = module.ImportReference(handler.CatchType);
        }
    }

    //ImportOperand 把一条指令的操作数导进目标模块
    //Cecil 写元数据时不会自己去别的模块解析引用 不导就会在落盘那一刻才炸
    private static void ImportOperand(ModuleDefinition module, Instruction instruction)
    {
        switch (instruction.Operand)
        {
            case TypeReference type:
                instruction.Operand = module.ImportReference(type);
                break;
            case MethodReference method:
                instruction.Operand = module.ImportReference(method);
                break;
            case FieldReference field:
                instruction.Operand = module.ImportReference(field);
                break;
        }
    }

    //FindField 在目标类型上按名字找回被搬走的字段定义
    private static FieldDefinition? FindField(TypeDefinition target, FieldReference field)
        => target.Fields.FirstOrDefault(f => f.Name == field.Name);

    //FindMethod 按名字与参数个数找 重载签名一致时找第一个 与原版按名字定位的粒度相同
    private static MethodDefinition? FindMethod(TypeDefinition target, MethodReference method)
        => target.Methods.FirstOrDefault(m => m.Name == method.Name && m.Parameters.Count == method.Parameters.Count);

    //MergeConstructors 把来源类型的字段初值并进目标的构造器
    //实例字段的初值要进每一个实例构造器 静态字段的进静态构造器 目标没有静态构造器就现造一个
    private static void MergeConstructors(TypeDefinition target, TypeDefinition source, ModuleDefinition module)
    {
        var instanceInit = ExtractInitializer(source.Methods.FirstOrDefault(m => m.IsConstructor && !m.IsStatic));
        if (instanceInit.Count > 0)
        {
            var constructors = target.Methods
                .Where(m => m.IsConstructor && !m.IsStatic && m.Body is not null)
                .ToList();
            foreach (var ctor in constructors)
            {
                var baseCall = FindBaseCall(ctor);
                if (baseCall is null)
                    continue;
                InsertInitializer(ctor, instanceInit, baseCall, insertBefore: false, source, target, module);
            }
        }

        var staticInit = ExtractInitializer(source.Methods.FirstOrDefault(m => m.IsConstructor && m.IsStatic));
        if (staticInit.Count > 0)
        {
            var cctor = target.Methods.FirstOrDefault(m => m.IsConstructor && m.IsStatic);
            if (cctor is null)
            {
                cctor = new MethodDefinition(".cctor",
                    MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.SpecialName |
                    MethodAttributes.RTSpecialName | MethodAttributes.HideBySig,
                    module.TypeSystem.Void);
                target.Methods.Add(cctor);
                cctor.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                cctor.Body.InitLocals = true;
            }

            var last = cctor.Body.Instructions.LastOrDefault(i => i.OpCode == OpCodes.Ret);
            if (last is not null)
                InsertInitializer(cctor, staticInit, last, insertBefore: true, source, target, module);
        }
    }

    //FindBaseCall 找构造器里链到基类或 this 的那次调用 字段初值要插在它后面
    private static Instruction? FindBaseCall(MethodDefinition ctor)
        => ctor.Body?.Instructions.FirstOrDefault(i =>
            i.OpCode == OpCodes.Call && i.Operand is MethodReference called && called.Name == ".ctor");

    //ExtractInitializer 从构造器里截出字段初始化那一段
    //剔掉链基类或 this 的那次调用 目标构造器里已经有它自己那一次 多调一次会把基类构造跑两遍
    //它前面压 this 的 ldarg.0 也一并剔掉
    //不能按"链构造器的调用在头还是在尾"来切 C# 把字段初值放在它前面还是后面都出现过 两种顺序都要吃得下
    //带异常处理表的构造器整块不搬 标签要跟着指令重排 收益不值这个风险
    private static List<Instruction> ExtractInitializer(MethodDefinition? ctor)
    {
        if (ctor?.Body is null || ctor.Body.HasExceptionHandlers)
            return new List<Instruction>();

        var instructions = ctor.Body.Instructions;
        var dropped = new HashSet<Instruction>();
        foreach (var instruction in instructions)
        {
            if (instruction.OpCode != OpCodes.Call && instruction.OpCode != OpCodes.Callvirt)
                continue;
            if (instruction.Operand is not MethodReference called || called.Name != ".ctor")
                continue;

            dropped.Add(instruction);
            var index = instructions.IndexOf(instruction);
            if (index > 0 && instructions[index - 1].OpCode == OpCodes.Ldarg_0)
                dropped.Add(instructions[index - 1]);
        }

        var result = new List<Instruction>();
        foreach (var instruction in instructions)
        {
            if (instruction.OpCode == OpCodes.Ret || dropped.Contains(instruction))
                continue;
            result.Add(instruction);
        }
        return result;
    }

    //InsertInitializer 把一段初始化指令克隆进宿主方法
    //克隆而不是搬 来源那一段可能要给多个构造器各插一份 顺带把变量与分支目标一起重排
    private static void InsertInitializer(MethodDefinition host, List<Instruction> block, Instruction anchor,
        bool insertBefore, TypeDefinition from, TypeDefinition to, ModuleDefinition module)
    {
        if (host.Body is null || block.Count == 0)
            return;

        //第一遍按原操作数建克隆 此时分支目标与引用还指着来源方法 Cecil 的 Create 按操作数类型校验 这样才建得出来
        var map = new Dictionary<Instruction, Instruction>();
        foreach (var instruction in block)
            map[instruction] = CloneWith(instruction.OpCode, instruction.Operand);

        //第二遍把它们扳到目标方法里的对应位置 顺手把外部引用导进目标模块
        var variables = new Dictionary<VariableDefinition, VariableDefinition>();
        foreach (var instruction in block)
        {
            map[instruction].Operand = RemapOperand(map[instruction].Operand, map, variables, host, from, to);
            ImportOperand(module, map[instruction]);
        }

        var il = host.Body.GetILProcessor();
        if (insertBefore)
        {
            foreach (var instruction in block)
                il.InsertBefore(anchor, map[instruction]);
            return;
        }

        //InsertAfter 是往锚点后面插 得自己推游标 否则多次调用会把顺序插反
        var cursor = anchor;
        foreach (var instruction in block)
        {
            var cloned = map[instruction];
            il.InsertAfter(cursor, cloned);
            cursor = cloned;
        }
    }

    //CloneWith 按操作数的具体类型挑 Create 重载
    //Cecil 没有接受任意 object 的重载 且会对 InlineNone 之外的 opcode 拒绝无参构造 所以必须按类型分发
    private static Instruction CloneWith(OpCode opcode, object? operand)
        => operand switch
        {
            null => Instruction.Create(opcode),
            TypeReference type => Instruction.Create(opcode, type),
            MethodReference method => Instruction.Create(opcode, method),
            FieldReference field => Instruction.Create(opcode, field),
            string text => Instruction.Create(opcode, text),
            sbyte value => Instruction.Create(opcode, value),
            byte value => Instruction.Create(opcode, value),
            int value => Instruction.Create(opcode, value),
            long value => Instruction.Create(opcode, value),
            float value => Instruction.Create(opcode, value),
            double value => Instruction.Create(opcode, value),
            Instruction target => Instruction.Create(opcode, target),
            //switch 表要连数组一起复制 不然后面重排会改到来源方法那一份
            Instruction[] targets => Instruction.Create(opcode, (Instruction[])targets.Clone()),
            VariableDefinition variable => Instruction.Create(opcode, variable),
            ParameterDefinition parameter => Instruction.Create(opcode, parameter),
            CallSite site => Instruction.Create(opcode, site),
            _ => Instruction.Create(opcode),
        };

    //RemapOperand 把克隆指令的操作数改指搬到目标之后的位置
    private static object? RemapOperand(object? operand, Dictionary<Instruction, Instruction> map,
        Dictionary<VariableDefinition, VariableDefinition> variables, MethodDefinition host,
        TypeDefinition from, TypeDefinition to)
    {
        switch (operand)
        {
            case null:
                return null;
            case Instruction[] targets:
                for (var i = 0; i < targets.Length; i++)
                {
                    if (map.TryGetValue(targets[i], out var mappedTarget))
                        targets[i] = mappedTarget;
                }
                return targets;
            case Instruction target:
                return map.TryGetValue(target, out var mapped) ? mapped : target;
            case VariableDefinition variable:
                if (!variables.TryGetValue(variable, out var cloned))
                {
                    //来源方法的局部变量在宿主里未必同名 每次遇到都补一个
                    cloned = new VariableDefinition(variable.VariableType);
                    variables[variable] = cloned;
                    host.Body.Variables.Add(cloned);
                    host.Body.InitLocals = true;
                }
                return cloned;
            case FieldReference field when field.DeclaringType?.FullName == from.FullName:
                return FindField(to, field) ?? field;
            case MethodReference called when called.DeclaringType?.FullName == from.FullName:
                return FindMethod(to, called) ?? called;
            default:
                return operand;
        }
    }

    //AddInterfaces 往目标类型的接口表里加接口
    private static void AddInterfaces(TypeDefinition target, MixinRule rule, ModuleDefinition module)
    {
        foreach (var reference in rule.Interfaces)
        {
            var typeRef = MakeTypeRef(module, reference);
            if (target.Interfaces.Any(i => i.InterfaceType.FullName == typeRef.FullName))
                continue;
            target.Interfaces.Add(new InterfaceImplementation(typeRef));
        }
    }

    //MakeTypeRef 按名字拼一条类型引用 全程不加载那个类型
    //模块里已有定义直接用它 在别的程序集就复用现成的程序集引用 没有才新建一条
    private static TypeReference MakeTypeRef(ModuleDefinition module, TypeRef reference)
    {
        var local = module.GetType(reference.TypeName);
        if (local is not null)
            return local;

        IMetadataScope? scope = null;
        if (!string.IsNullOrEmpty(reference.AssemblyName))
        {
            var existing = module.AssemblyReferences.FirstOrDefault(r => r.Name == reference.AssemblyName);
            if (existing is null)
            {
                //没有现成引用时版本只能给 0 CLR 对同简单名的程序集不强求版本一致
                existing = new AssemblyNameReference(reference.AssemblyName, new Version(0, 0, 0, 0));
                module.AssemblyReferences.Add(existing);
            }
            scope = existing;
        }

        //嵌套类型暂不支持 全名按最后一段点号切命名空间与名字
        var split = reference.TypeName.LastIndexOf('.');
        var space = split < 0 ? string.Empty : reference.TypeName[..split];
        var name = split < 0 ? reference.TypeName : reference.TypeName[(split + 1)..];
        return new TypeReference(space, name, module, scope);
    }
}
