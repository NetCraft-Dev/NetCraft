using System.Collections;
using System.Numerics;

namespace NetCraft.Util;

//动态位集对应原版java.util.BitSet
//基于long[]按位存储支持任意非负下标
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

    //已设置的最高位所在word数+1对应原版size
    public int Size => _wordCount << WordShift;

    //是否未设置任何位对应原版isEmpty
    public bool IsEmpty => _wordCount == 0;

    //获取index位对应原版get
    public bool Get(int index)
    {
        if (index < 0) throw new IndexOutOfRangeException($"Index {index} out of range");
        var wordIndex = index >> WordShift;
        if (wordIndex >= _wordCount) return false;
        return (_words[wordIndex] & (1L << index)) != 0;
    }

    //设置index位对应原版set
    public void Set(int index)
    {
        if (index < 0) throw new IndexOutOfRangeException($"Index {index} out of range");
        var wordIndex = index >> WordShift;
        EnsureCapacity(wordIndex + 1);
        _words[wordIndex] |= 1L << index;
        if (wordIndex >= _wordCount) _wordCount = wordIndex + 1;
    }

    //设置index位为value对应原版set(int,boolean)
    public void Set(int index, bool value)
    {
        if (value) Set(index);
        else Clear(index);
    }

    //判断与other是否有交集对应原版intersects
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

    //清空index位对应原版clear
    public void Clear(int index)
    {
        var wordIndex = index >> WordShift;
        if (wordIndex >= _wordCount) return;
        _words[wordIndex] &= ~(1L << index);
        TrimWordCount();
    }

    //扩容到newCapacity个word
    private void EnsureCapacity(int newWordCount)
    {
        if (newWordCount <= _words.Length) return;
        var newSize = Math.Max(newWordCount, _words.Length * 2);
        Array.Resize(ref _words, newSize);
    }

    //clone返回内容相同的副本对应原版java.util.BitSet.clone
    public BitSet Clone()
    {
        var copy = new BitSet(_wordCount << WordShift);
        Array.Copy(_words, copy._words, _wordCount);
        copy._wordCount = _wordCount;
        return copy;
    }

    //回收尾部全零的word并更新_wordCount
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
