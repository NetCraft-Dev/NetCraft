using System.Collections;
using System.Numerics;

namespace NetCraft.Util;

//Dynamic bit set, maps to vanilla java.util.BitSet
//Bit-packed storage over long[], supports arbitrary non-negative indices
public sealed class BitSet
{
    private const int BitsPerWord = 64;
    private const int WordShift = 6;
    private const long WordMask = -1L;
    private long[] _words;
    private int _wordCount;

    public BitSet() : this(64) { }

    public BitSet(int capacity)
    {
        var wordCount = Math.Max(1, (capacity + BitsPerWord - 1) >> WordShift);
        _words = new long[wordCount];
        _wordCount = 0;
    }

    //Highest set bit's word index + 1, maps to vanilla size
    public int Size => _wordCount << WordShift;

    //Whether no bit is set, maps to vanilla isEmpty
    public bool IsEmpty => _wordCount == 0;

    //Reads the bit at index, maps to vanilla get
    public bool Get(int index)
    {
        if (index < 0) throw new IndexOutOfRangeException($"Index {index} out of range");
        var wordIndex = index >> WordShift;
        if (wordIndex >= _wordCount) return false;
        return (_words[wordIndex] & (1L << index)) != 0;
    }

    //Sets the bit at index, maps to vanilla set
    public void Set(int index)
    {
        if (index < 0) throw new IndexOutOfRangeException($"Index {index} out of range");
        var wordIndex = index >> WordShift;
        EnsureCapacity(wordIndex + 1);
        _words[wordIndex] |= 1L << index;
        if (wordIndex >= _wordCount) _wordCount = wordIndex + 1;
    }

    //Sets the bit at index to value, maps to vanilla set(int,boolean)
    public void Set(int index, bool value)
    {
        if (value) Set(index);
        else Clear(index);
    }

    //Tests for intersection with other, maps to vanilla intersects
    public bool Intersects(BitSet other)
    {
        if (other is null) return false;
        var limit = Math.Min(_wordCount, other._wordCount);
        for (int i = 0; i < limit; i++)
        {
            if ((_words[i] & other._words[i]) != 0L) return true;
        }
        return false;
    }

    //Clears the bit at index, maps to vanilla clear
    public void Clear(int index)
    {
        var wordIndex = index >> WordShift;
        if (wordIndex >= _wordCount) return;
        _words[wordIndex] &= ~(1L << index);
        TrimWordCount();
    }

    //Grows to newCapacity words
    private void EnsureCapacity(int newWordCount)
    {
        if (newWordCount <= _words.Length) return;
        var newSize = Math.Max(newWordCount, _words.Length * 2);
        Array.Resize(ref _words, newSize);
    }

    //clone returns an identical copy, maps to vanilla java.util.BitSet.clone
    public BitSet Clone()
    {
        var copy = new BitSet(_wordCount << WordShift);
        Array.Copy(_words, copy._words, _wordCount);
        copy._wordCount = _wordCount;
        return copy;
    }

    //Reclaims trailing all-zero words and updates _wordCount
    private void TrimWordCount()
    {
        while (_wordCount > 0 && _words[_wordCount - 1] == 0L)
            _wordCount--;
    }

    public IEnumerator<int> GetEnumerator()
    {
        for (var i = 0; i < _wordCount; i++)
        {
            var word = _words[i];
            while (word != 0)
            {
                var bitIndex = BitOperations.TrailingZeroCount(word);
                yield return (i << WordShift) | bitIndex;
                word &= word - 1;
            }
        }
    }
}
