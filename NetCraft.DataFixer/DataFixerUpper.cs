namespace NetCraft.DataFixer;

using System;
using System.Collections.Generic;
using NetCraft.Codec;
using NetCraft.DataFixer.Functions;
using NetCraft.DataFixer.Schemas;
using NetCraft.DataFixer.Types;

//DataFixerUpper核心修复器对应原版DataFixerUpper
//管理Schema列表与DataFix列表按版本组合规则
public sealed class DataFixerUpper : DataFixer
{
    //ERRORS_ARE_FATAL是否将错误视为致命
    public static bool ERRORS_ARE_FATAL;

    //OPTIMIZATION_RULE全局函数优化规则
    //原版用CataFuseSame/CataFuseDifferent/LensComp/SortProj/SortInj/AppNest组合规则
    //C#这些高级PointFree规则未移植先用Nop占位阶段C接通MC集成层时实现
    public static readonly PointFreeRule OPTIMIZATION_RULE = PointFreeRule.Nop();

    private readonly SortedDictionary<int, Schema> _schemas;
    private readonly List<DataFix> _globalList;
    private readonly SortedSet<int> _fixerVersions;
    private readonly Dictionary<long, TypeRewriteRule> _rules = new();

    internal DataFixerUpper(SortedDictionary<int, Schema> schemas, List<DataFix> globalList, SortedSet<int> fixerVersions)
    {
        _schemas = schemas;
        _globalList = globalList;
        _fixerVersions = fixerVersions;
    }

    //update按type与版本范围对input执行更新对应原版DataFixerUpper.update
    //version<newVersion时按type读取input经规则重写后用新类型编码回T
    public Dynamic<T> Update<T>(DSL.ITypeReference type, Dynamic<T> input, int version, int newVersion)
    {
        if (version < newVersion)
        {
            var dataType = GetType(type, version);
            var expectedType = GetType(type, newVersion);
            var rule = GetRule(version, newVersion);
            var read = dataType.ReadAndWrite(input.Ops, expectedType, rule, OPTIMIZATION_RULE, input.Value);
            var result = read.Result().IsPresent ? read.GetOrThrow() : input.Value;
            return new Dynamic<T>(input.Ops, result);
        }
        return input;
    }

    //getSchema按key取得同版本最低Schema
    public Schema GetSchema(int key)
        => _schemas[DataFixerBuilder.GetLowestSchemaSameVersion(_schemas, key)];

    //getType按引用与版本取得类型委托Schema.GetTypeRaw
    public Type<object> GetType(DSL.ITypeReference type, int version)
        => GetSchema(DataFixUtils.MakeKey(version)).GetTypeRaw(type);

    //getRule按版本范围组合所有相关DataFix规则用long key缓存
    public TypeRewriteRule GetRule(int version, int newVersion)
    {
        if (version >= newVersion) return TypeRewriteRule.Nop();
        long key = (long)version << 32 | (uint)newVersion;
        if (_rules.TryGetValue(key, out var cached)) return cached;
        int expandedVersion = GetLowestFixSameVersion(DataFixUtils.MakeKey(version));
        var rules = new List<TypeRewriteRule>();
        foreach (var fix in _globalList)
        {
            int expandedFixVersion = fix.GetVersionKey();
            int fixVersion = DataFixUtils.GetVersion(expandedFixVersion);
            if (expandedFixVersion > expandedVersion && fixVersion <= newVersion)
            {
                var fixRule = fix.GetRule();
                Console.Error.WriteLine($"DEBUG GetRule fix={fix.GetType().Name} fixVersion={fixVersion} included={!ReferenceEquals(fixRule, TypeRewriteRule.Nop())}");
                if (ReferenceEquals(fixRule, TypeRewriteRule.Nop())) continue;
                rules.Add(fixRule);
            }
        }
        var combined = TypeRewriteRule.Seq(rules);
        _rules[key] = combined;
        return combined;
    }

    //getLowestFixSameVersion返回不超过versionKey同版本最大fixer版本
    private int GetLowestFixSameVersion(int versionKey)
    {
        int first = FirstFixerVersion();
        if (versionKey < first) return first - 1;
        int result = first;
        foreach (var k in _fixerVersions)
        {
            if (k > versionKey) break;
            result = k;
        }
        return result;
    }

    private int FirstFixerVersion()
    {
        foreach (var k in _fixerVersions) return k;
        return 0;
    }

    //fixerVersions返回已注册修复版本集合
    public SortedSet<int> FixerVersions() => _fixerVersions;
}
