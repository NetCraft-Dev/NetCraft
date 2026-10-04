namespace NetCraft.DataFixer.Types.Families;

using System;
using System.Collections.Generic;
using NetCraft.DataFixer;
using NetCraft.DataFixer.Functions;

//ListAlgebra列表代数对应原版ListAlgebra
//把RewriteResult列表包装为Algebra按索引取值
public sealed class ListAlgebra : Algebra
{
    private readonly string _name;
    private readonly List<RewriteResult<object, object>> _views;
    private int _hashCode;

    public ListAlgebra(string name, List<RewriteResult<object, object>> views)
    {
        _name = name;
        _views = views;
    }

    public RewriteResult<object, object> Apply(int index) => _views[index];

    public override string ToString() => ToString(0);

    public string ToString(int level)
    {
        var wrap = "\n" + PointFree<object>.Indent(level + 1);
        return "Algebra[" + _name + wrap + "]" + PointFree<object>.Indent(level) + "]";
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj)) return true;
        if (obj is not ListAlgebra that) return false;
        return Equals(_views, that._views);
    }

    public override int GetHashCode()
    {
        if (_hashCode == 0) _hashCode = _views?.GetHashCode() ?? 0;
        return _hashCode;
    }
}
