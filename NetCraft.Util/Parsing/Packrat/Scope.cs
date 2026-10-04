using System.Text;

namespace NetCraft.Util.Parsing.Packrat;

//作用域栈对应原版net.minecraft.util.parsing.packrat.Scope
//用Object[]数组模拟栈帧pushFrame/popFrame管理嵌套规则作用域
//splitFrame/mergeFrame支持Alternative分支尝试
public sealed class Scope
{
    private static readonly object FrameStartMarker = new();

    private object?[] _stack = new object?[128];
    private int _topEntryKeyIndex = 0;
    private int _topMarkerKeyIndex = 0;

    public Scope()
    {
        _stack[0] = FrameStartMarker;
        _stack[1] = null;
    }

    private int ValueIndex(Atom atom)
    {
        for (var i = _topEntryKeyIndex; i > _topMarkerKeyIndex; i -= 2)
        {
            if (_stack[i] == atom) return i + 1;
        }
        return -1;
    }

    public int ValueIndexForAny(params Atom[] atoms)
    {
        for (var i = _topEntryKeyIndex; i > _topMarkerKeyIndex; i -= 2)
        {
            var key = _stack[i];
            foreach (var atom in atoms)
            {
                if (key == atom) return i + 1;
            }
        }
        return -1;
    }

    private void EnsureCapacity(int additionalEntryCount)
    {
        var currentSize = _stack.Length;
        var currentLastValueIndex = _topEntryKeyIndex + 1;
        var newLastValueIndex = currentLastValueIndex + additionalEntryCount * 2;
        if (newLastValueIndex >= currentSize)
        {
            var newSize = GrowByHalf(currentSize, newLastValueIndex + 1);
            var newStack = new object[newSize];
            Array.Copy(_stack, newStack, currentSize);
            _stack = newStack;
        }
    }

    private static int GrowByHalf(int current, int needed)
        => Math.Max(current + (current >> 1), needed);

    private void SetupNewFrame()
    {
        _topEntryKeyIndex += 2;
        _stack[_topEntryKeyIndex] = FrameStartMarker;
        _stack[_topEntryKeyIndex + 1] = _topMarkerKeyIndex;
        _topMarkerKeyIndex = _topEntryKeyIndex;
    }

    public void PushFrame()
    {
        EnsureCapacity(1);
        SetupNewFrame();
    }

    private int GetPreviousMarkerIndex(int markerKeyIndex)
        => (int)_stack[markerKeyIndex + 1]!;

    public void PopFrame()
    {
        _topEntryKeyIndex = _topMarkerKeyIndex - 2;
        _topMarkerKeyIndex = GetPreviousMarkerIndex(_topMarkerKeyIndex);
    }

    public void SplitFrame()
    {
        var currentFrameMarkerIndex = _topMarkerKeyIndex;
        var nonMarkerEntriesInFrame = (_topEntryKeyIndex - _topMarkerKeyIndex) / 2;
        EnsureCapacity(nonMarkerEntriesInFrame + 1);
        SetupNewFrame();
        var sourceCursor = currentFrameMarkerIndex + 2;
        var targetCursor = _topEntryKeyIndex;
        for (var i = 0; i < nonMarkerEntriesInFrame; i++)
        {
            targetCursor += 2;
            var key = _stack[sourceCursor];
            _stack[targetCursor] = key;
            _stack[targetCursor + 1] = null;
            sourceCursor += 2;
        }
        _topEntryKeyIndex = targetCursor;
    }

    public void ClearFrameValues()
    {
        for (var i = _topEntryKeyIndex; i > _topMarkerKeyIndex; i -= 2)
        {
            _stack[i + 1] = null;
        }
    }

    public void MergeFrame()
    {
        var previousMarkerIndex = GetPreviousMarkerIndex(_topMarkerKeyIndex);
        var previousFrameCursor = previousMarkerIndex;
        var currentFrameCursor = _topMarkerKeyIndex;
        while (currentFrameCursor < _topEntryKeyIndex)
        {
            previousFrameCursor += 2;
            currentFrameCursor += 2;
            var newKey = _stack[currentFrameCursor];
            var newValue = _stack[currentFrameCursor + 1];
            var oldKey = _stack[previousFrameCursor];
            if (oldKey != newKey)
            {
                _stack[previousFrameCursor] = newKey;
                _stack[previousFrameCursor + 1] = newValue;
            }
            else if (newValue is not null)
            {
                _stack[previousFrameCursor + 1] = newValue;
            }
        }
        _topEntryKeyIndex = previousFrameCursor;
        _topMarkerKeyIndex = previousMarkerIndex;
    }

    public void Put<T>(Atom<T> name, T? value)
    {
        var valueIndex = ValueIndex(name);
        if (valueIndex != -1)
        {
            _stack[valueIndex] = value;
        }
        else
        {
            EnsureCapacity(1);
            _topEntryKeyIndex += 2;
            _stack[_topEntryKeyIndex] = name;
            _stack[_topEntryKeyIndex + 1] = value;
        }
    }

    public T? Get<T>(Atom<T> atom)
    {
        var i = ValueIndex(atom);
        return i != -1 ? (T?)_stack[i] : default;
    }

    public T GetOrThrow<T>(Atom<T> atom)
    {
        var i = ValueIndex(atom);
        if (i == -1) throw new ArgumentException("No value for atom " + atom);
        return (T)_stack[i]!;
    }

    public T GetOrDefault<T>(Atom<T> atom, T def)
    {
        var i = ValueIndex(atom);
        return i != -1 ? (T)_stack[i]! : def;
    }

    public T? GetAny<T>(params Atom[] atoms)
    {
        var i = ValueIndexForAny(atoms);
        return i != -1 ? (T?)_stack[i] : default;
    }

    public T GetAnyOrThrow<T>(params Atom[] atoms)
    {
        var i = ValueIndexForAny(atoms);
        if (i == -1) throw new ArgumentException("No value for atoms " + atoms);
        return (T)_stack[i]!;
    }

    public override string ToString()
    {
        var result = new StringBuilder();
        var afterFrame = true;
        for (var i = 0; i <= _topEntryKeyIndex; i += 2)
        {
            var key = _stack[i];
            var value = _stack[i + 1];
            if (key == FrameStartMarker)
            {
                result.Append('|');
                afterFrame = true;
            }
            else
            {
                if (!afterFrame) result.Append(',');
                afterFrame = false;
                result.Append(key).Append(':').Append(value);
            }
        }
        return result.ToString();
    }

    public Dictionary<Atom, object?> LastFrame()
    {
        var result = new Dictionary<Atom, object?>();
        for (var i = _topEntryKeyIndex; i > _topMarkerKeyIndex; i -= 2)
        {
            var key = (Atom)_stack[i]!;
            var value = _stack[i + 1];
            result[key] = value;
        }
        return result;
    }

    public bool HasOnlySingleFrame()
    {
        for (var i = _topEntryKeyIndex; i > 0; i--)
        {
            if (_stack[i] == FrameStartMarker) return false;
        }
        if (_stack[0] != FrameStartMarker) throw new InvalidOperationException("Corrupted stack");
        return true;
    }
}
