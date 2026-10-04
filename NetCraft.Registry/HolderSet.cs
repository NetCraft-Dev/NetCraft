namespace NetCraft.Registry;

//HolderSet对应原版net.minecraft.core.HolderSet
//包装一组Holder按标签或直接列表形式
public interface HolderSet<T> : IEnumerable<Holder<T>> where T : class
{
    int Size { get; }

    //已绑定具体内容Direct恒true Named未bind返回false
    bool IsBound { get; }

    //解包标签键Named返回TagKey Direct返回null
    TagKey<T>? UnwrapKey();

    Holder<T> Get(int index);

    bool Contains(Holder<T> value);
}

//ListBacked基于列表的抽象实现提供Size/Get/迭代默认行为
public abstract class ListBackedHolderSet<T> : HolderSet<T> where T : class
{
    protected abstract IReadOnlyList<Holder<T>> Contents { get; }

    public int Size => Contents.Count;

    public Holder<T> Get(int index) => Contents[index];

    public IEnumerator<Holder<T>> GetEnumerator() => Contents.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    //子类必须实现IsBound/UnwrapKey/Contains对应原版ListBacked未提供默认
    public abstract bool IsBound { get; }
    public abstract TagKey<T>? UnwrapKey();
    public abstract bool Contains(Holder<T> value);
}

//DirectHolderSet直接包装Holder列表不可变
public sealed class DirectHolderSet<T> : ListBackedHolderSet<T> where T : class
{
    //每个封闭泛型类型各持一份空集 若共用 object 版再强转 对 T != object 会抛 InvalidCastException
    private static readonly DirectHolderSet<T> _empty = new(Array.Empty<Holder<T>>());

    private readonly IReadOnlyList<Holder<T>> _contents;
    private HashSet<Holder<T>>? _contentsSet;

    public DirectHolderSet(IReadOnlyList<Holder<T>> contents)
    {
        _contents = contents;
    }

    public static DirectHolderSet<T> Empty => (DirectHolderSet<T>)(object)_empty;

    protected override IReadOnlyList<Holder<T>> Contents => _contents;

    public override bool IsBound => true;

    public override TagKey<T>? UnwrapKey() => null;

    public override bool Contains(Holder<T> value)
    {
        _contentsSet ??= new(_contents, ReferenceEqualityComparer.Instance);
        return _contentsSet.Contains(value);
    }

    public override string ToString() => "DirectSet[" + string.Join(", ", _contents) + "]";
}

//NamedHolderSet按TagKey绑定可变内容对应原版HolderSet.Named
public sealed class NamedHolderSet<T> : ListBackedHolderSet<T> where T : class
{
    private readonly HolderOwner<T> _owner;
    private readonly TagKey<T> _key;
    private IReadOnlyList<Holder<T>>? _contents;

    public NamedHolderSet(HolderOwner<T> owner, TagKey<T> key)
    {
        _owner = owner;
        _key = key;
    }

    public TagKey<T> Key => _key;

    //Bind绑定具体Holder列表由MappedRegistry.bindTags调用
    internal void Bind(IReadOnlyList<Holder<T>> contents)
    {
        _contents = contents;
    }

    protected override IReadOnlyList<Holder<T>> Contents
        => _contents ?? throw new InvalidOperationException($"Trying to access unbound tag '{_key}' from registry {_owner}");

    public override bool IsBound => _contents is not null;

    public override TagKey<T>? UnwrapKey() => _key;

    public override bool Contains(Holder<T> value) => value.Is(_key);

    public override string ToString() => $"NamedSet({_key})[{(_contents is null ? "<unbound>" : string.Join(", ", _contents))}]";
}
