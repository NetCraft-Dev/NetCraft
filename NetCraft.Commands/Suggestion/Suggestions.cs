using NetCraft.Commands.Context;

namespace NetCraft.Commands.Suggestion;

//Suggestions 建议集合对应原版com.mojang.brigadier.suggestion.Suggestions
//聚合多个Suggestion按range扩展并按忽略大小写排序供补全展示
public sealed class Suggestions : IEquatable<Suggestions>
{
    //EmptyInstance 空建议共享实例由Empty()包装为Task返回
    private static readonly Suggestions _emptyInstance = new(StringRange.At(0), new List<Suggestion>());
    private static readonly Task<Suggestions> _empty = Task.FromResult(_emptyInstance);

    private readonly StringRange _range;
    private readonly List<Suggestion> _suggestions;

    public Suggestions(StringRange range, List<Suggestion> suggestions)
    {
        _range = range;
        _suggestions = suggestions;
    }

    public StringRange Range => _range;
    public IReadOnlyList<Suggestion> List => _suggestions;

    public bool IsEmpty() => _suggestions.Count == 0;

    public bool Equals(Suggestions? other)
    {
        if (other is null)
        {
            return false;
        }
        if (ReferenceEquals(this, other))
        {
            return true;
        }
        if (_range != other._range)
        {
            return false;
        }
        if (_suggestions.Count != other._suggestions.Count)
        {
            return false;
        }
        for (var i = 0; i < _suggestions.Count; i++)
        {
            if (!_suggestions[i].Equals(other._suggestions[i]))
            {
                return false;
            }
        }
        return true;
    }

    public override bool Equals(object? obj) => obj is Suggestions other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = _range.GetHashCode();
            foreach (var s in _suggestions)
            {
                hash = (hash * 31) ^ (s?.GetHashCode() ?? 0);
            }
            return hash;
        }
    }

    public override string ToString()
    {
        return "Suggestions{range=" + _range + ", suggestions=" + string.Join(", ", _suggestions) + "}";
    }

    public static Task<Suggestions> Empty() => _empty;

    //Merge 合并多个Suggestions去重后交给Create统一扩展排序
    public static Task<Suggestions> Merge(string command, ICollection<Suggestions> input)
    {
        if (input.Count == 0)
        {
            return Empty();
        }
        if (input.Count == 1)
        {
            using var e = input.GetEnumerator();
            e.MoveNext();
            return Task.FromResult(e.Current);
        }
        var texts = new HashSet<Suggestion>();
        foreach (var suggestions in input)
        {
            foreach (var s in suggestions.List)
            {
                texts.Add(s);
            }
        }
        return Create(command, texts);
    }

    //Create 计算suggestions的最小range并扩展文本到统一range再按忽略大小写排序
    public static Task<Suggestions> Create(string command, ICollection<Suggestion> suggestions)
    {
        if (suggestions.Count == 0)
        {
            return Empty();
        }
        var start = int.MaxValue;
        var end = int.MinValue;
        foreach (var suggestion in suggestions)
        {
            start = Math.Min(suggestion.Range.Start, start);
            end = Math.Max(suggestion.Range.End, end);
        }
        var range = new StringRange(start, end);
        var texts = new HashSet<Suggestion>();
        foreach (var suggestion in suggestions)
        {
            texts.Add(suggestion.Expand(command, range));
        }
        var sorted = new List<Suggestion>(texts);
        sorted.Sort((a, b) => a.CompareToIgnoreCase(b));
        return Task.FromResult(new Suggestions(range, sorted));
    }
}
