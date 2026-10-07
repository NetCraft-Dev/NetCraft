namespace NetCraft.DataFixer;

using System;
using System.Collections.Generic;
using NetCraft.Codec;
using NetCraft.DataFixer.Functions;
using NetCraft.DataFixer.Schemas;
using NetCraft.DataFixer.Types;

//DataFixerUpper core fixer, maps to vanilla DataFixerUpper
//manages the Schema list and DataFix list, combining rules by version
public sealed class DataFixerUpper : DataFixer
{
    //ERRORS_ARE_FATAL whether errors are treated as fatal
    public static bool ERRORS_ARE_FATAL;

    //OPTIMIZATION_RULE global function optimization rule
    //vanilla combines CataFuseSame/CataFuseDifferent/LensComp/SortProj/SortInj/AppNest rules
    //these advanced PointFree rules are not ported in C# yet, so Nop is a placeholder until stage C wires up the MC integration layer
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

    //update applies updates to input by type and version range, maps to vanilla DataFixerUpper.update
    //when version<newVersion, reads input by type, rewrites it via the rule, then re-encodes into T with the new type
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

    //getSchema gets the lowest Schema of the same version by key
    public Schema GetSchema(int key)
        => _schemas[DataFixerBuilder.GetLowestSchemaSameVersion(_schemas, key)];

    //getType gets the type by reference and version, delegating to Schema.GetTypeRaw
    public Type<object> GetType(DSL.ITypeReference type, int version)
        => GetSchema(DataFixUtils.MakeKey(version)).GetTypeRaw(type);

    //getRule combines all relevant DataFix rules over a version range, cached by a long key
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

    //getLowestFixSameVersion returns the largest fixer version of the same version not exceeding versionKey
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

    //fixerVersions returns the set of registered fixer versions
    public SortedSet<int> FixerVersions() => _fixerVersions;
}
