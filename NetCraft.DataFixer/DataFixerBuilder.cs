namespace NetCraft.DataFixer;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NetCraft.DataFixer.Schemas;

//DataFixerBuilder修复器构造器对应原版DataFixerBuilder
//累积Schema与DataFix构建DataFixerUpper
public class DataFixerBuilder
{
    private readonly int _dataVersion;
    private readonly SortedDictionary<int, Schema> _schemas = new();
    private readonly List<DataFix> _globalList = new();
    private readonly SortedSet<int> _fixerVersions = new();

    public DataFixerBuilder(int dataVersion)
    {
        _dataVersion = dataVersion;
    }

    //addSchema按版本与工厂构造并注册Schema
    public Schema AddSchema(int version, Func<int, Schema?, Schema> factory)
        => AddSchema(version, 0, factory);

    public Schema AddSchema(int version, int subVersion, Func<int, Schema?, Schema> factory)
    {
        int key = DataFixUtils.MakeKey(version, subVersion);
        Schema? parent = null;
        if (_schemas.Count > 0)
        {
            int parentKey = GetLowestSchemaSameVersion(_schemas, key - 1);
            _schemas.TryGetValue(parentKey, out parent);
        }
        Schema schema = factory(key, parent);
        AddSchema(schema);
        return schema;
    }

    public void AddSchema(Schema schema)
    {
        _schemas[schema.GetVersionKey()] = schema;
    }

    //addFixer注册DataFix超出dataVersion时警告
    public void AddFixer(DataFix fix)
    {
        int version = DataFixUtils.GetVersion(fix.GetVersionKey());
        if (version > _dataVersion) return;
        _globalList.Add(fix);
        _fixerVersions.Add(fix.GetVersionKey());
    }

    //build构造最终DataFixerUpper
    public Result Build()
    {
        var fixer = new DataFixerUpper(new SortedDictionary<int, Schema>(_schemas), new List<DataFix>(_globalList), new SortedSet<int>(_fixerVersions));
        return new Result(fixer);
    }

    //getLowestSchemaSameVersion返回不超过key同版本最低Schema的版本号
    public static int GetLowestSchemaSameVersion(SortedDictionary<int, Schema> schemas, int versionKey)
    {
        if (schemas.Count == 0) return versionKey;
        int first = GetFirstKey(schemas);
        if (versionKey < first) return first;
        int result = first;
        foreach (var k in schemas.Keys)
        {
            if (k > versionKey) break;
            result = k;
        }
        return result;
    }

    private static int GetFirstKey(SortedDictionary<int, Schema> schemas)
    {
        foreach (var k in schemas.Keys) return k;
        return 0;
    }

    //Result构建结果包含fixer与optimize入口
    public sealed class Result
    {
        private readonly DataFixerUpper _fixerUpper;

        public Result(DataFixerUpper fixerUpper)
        {
            _fixerUpper = fixerUpper;
        }

        public DataFixer Fixer() => _fixerUpper;

        //optimize按需类型与执行器异步优化规则对应原版Result.optimize
        //原版用OPTIMIZATION_RULE对requiredTypes并行优化C#高级规则未移植先用CompletedTask占位
        public Task Optimize(HashSet<DSL.ITypeReference> requiredTypes, TaskScheduler executor)
            => Task.CompletedTask;
    }
}
