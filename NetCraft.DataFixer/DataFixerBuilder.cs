namespace NetCraft.DataFixer;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NetCraft.DataFixer.Schemas;

//DataFixerBuilder builder maps to vanilla DataFixerBuilder
//accumulates Schemas and DataFixes to build a DataFixerUpper
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

    //addSchema constructs and registers a Schema by version and factory
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

    //addFixer registers a DataFix; warns when it exceeds dataVersion
    public void AddFixer(DataFix fix)
    {
        int version = DataFixUtils.GetVersion(fix.GetVersionKey());
        if (version > _dataVersion) return;
        _globalList.Add(fix);
        _fixerVersions.Add(fix.GetVersionKey());
    }

    //build constructs the final DataFixerUpper
    public Result Build()
    {
        var fixer = new DataFixerUpper(new SortedDictionary<int, Schema>(_schemas), new List<DataFix>(_globalList), new SortedSet<int>(_fixerVersions));
        return new Result(fixer);
    }

    //getLowestSchemaSameVersion returns the version of the lowest same-version Schema not exceeding key
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

    //Result build result containing the fixer and optimize entry
    public sealed class Result
    {
        private readonly DataFixerUpper _fixerUpper;

        public Result(DataFixerUpper fixerUpper)
        {
            _fixerUpper = fixerUpper;
        }

        public DataFixer Fixer() => _fixerUpper;

        //optimize asynchronously optimizes rules for required types and an executor, maps to vanilla Result.optimize
        //vanilla uses OPTIMIZATION_RULE to optimize requiredTypes in parallel; the C# advanced rules are not ported yet, so CompletedTask is a placeholder
        public Task Optimize(HashSet<DSL.ITypeReference> requiredTypes, TaskScheduler executor)
            => Task.CompletedTask;
    }
}
