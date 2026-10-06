namespace NetCraft.Registry;

//HolderSet, maps to vanilla net.minecraft.core.HolderSet
//Wraps a group of Holders, as a tag or a direct list
public interface HolderSet<T> : IEnumerable<Holder<T>> where T : class
{
    int Size { get; }

    //Whether concrete contents are bound; always true for Direct and false for an unbound Named
    bool IsBound { get; }

    //Unwrap to a tag key; Named returns the TagKey and Direct returns null
    TagKey<T>? UnwrapKey();

    Holder<T> Get(int index);

    bool Contains(Holder<T> value);
}

//ListBacked list-based abstract implementation, providing default Size/Get/iteration behavior
public abstract class ListBackedHolderSet<T> : HolderSet<T> where T : class
{
    protected abstract IReadOnlyList<Holder<T>> Contents { get; }

    public int Size => Contents.Count;

    public Holder<T> Get(int index) => Contents[index];

    public IEnumerator<Holder<T>> GetEnumerator() => Contents.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    //Subclasses must implement IsBound/UnwrapKey/Contains; vanilla ListBacked provides no defaults
    public abstract bool IsBound { get; }
    public abstract TagKey<T>? UnwrapKey();
    public abstract bool Contains(Holder<T> value);
}

//DirectHolderSet directly wraps an immutable Holder list
public sealed class DirectHolderSet<T> : ListBackedHolderSet<T> where T : class
{
    //Each closed generic type keeps its own empty set; sharing an object version and casting would throw InvalidCastException for T != object
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

//NamedHolderSet binds mutable contents by TagKey, maps to vanilla HolderSet.Named
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

    //Bind binds the concrete Holder list, called by MappedRegistry.bindTags
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
