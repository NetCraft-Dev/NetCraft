using System.Runtime.CompilerServices;
using NetCraft.Client.Blaze3d.Systems;
using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Textures;
using NetCraft.Client.Blaze3d.Shaders;
using NetCraft.Client.Blaze3d.Pipeline;
using NetCraft.Client.Blaze3d.Vertex;
using NetCraft.Client.Blaze3d.Vulkan;
using NetCraft.Client.Blaze3d.Platform;
using NetCraft.Client.Blaze3d.Font;
using NetCraft.Client.Blaze3d.Resource;
using NetCraft.Client.Blaze3d.Audio;
using NetCraft.Client.Blaze3d.Framegraph;
using NetCraft.Client.Blaze3d.Preprocessor;
using NetCraft.Client.Blaze3d.Util;
using NetCraft.Client.Render;
using NetCraft.Client.Render.Model;
using NetCraft.Client.Render.Texture;
using NetCraft.Client.Render.Texture.Atlas;
using NetCraft.Client.Render.Item;
using NetCraft.Client.Render.Entity;
using NetCraft.Client.Render.Entity.State;
using NetCraft.Client.Render.State.Gui;
using NetCraft.Client.Gui;
using NetCraft.Client.Gui.Render;
using NetCraft.Client.Gui.Render.State;
using NetCraft.Client.Gui.Render.Pip;
using NetCraft.Client.Gui.Navigation;
using NetCraft.Client.Gui.Layouts;
using NetCraft.Client.Gui.Font;
using NetCraft.Client.Gui.Font.Providers;
using NetCraft.Client.Gui.Font.Glyphs;
using NetCraft.Client.Model;
using NetCraft.Client.Model.Geom;
using NetCraft.Client.Resources.Metadata.Gui;

namespace NetCraft.Client.Render.Texture.Atlas;

//BitSet fixed-length bit set, maps to java.util.BitSet, used by DynamicAtlasAllocator
//Set/Clear/NextSetBit O(1) with internal storage bucketed by ulong
public sealed class BitSet
{
    private readonly ulong[] _bits;
    private readonly int _size;

    public BitSet(int size)
    {
        _size = size;
        _bits = new ulong[(size + 63) >> 6];
    }

    //Set sets the bits in the range [from,to) to 1
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(int from, int to)
    {
        for (var i = from; i < to; i++) Set(i);
    }

    //Set sets a single bit to 1
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(int index)
    {
        if ((uint)index >= (uint)_size) return;
        _bits[index >> 6] |= 1UL << (index & 63);
    }

    //Clear sets a single bit to 0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear(int index)
    {
        if ((uint)index >= (uint)_size) return;
        _bits[index >> 6] &= ~(1UL << (index & 63));
    }

    //NextSetBit finds the next bit set to 1 starting at from and returns its index, or -1 if none
    //Uses System.Numerics.BitOperations.TrailingZeroCount to compute the lowest set bit
    public int NextSetBit(int from)
    {
        if (from < 0) from = 0;
        if (from >= _size) return -1;
        var wordIndex = from >> 6;
        var bitInWord = from & 63;
        var word = _bits[wordIndex] & (~0UL << bitInWord);
        while (true)
        {
            if (word != 0)
            {
                var idx = (wordIndex << 6) + System.Numerics.BitOperations.TrailingZeroCount(word);
                return idx < _size ? idx : -1;
            }
            wordIndex++;
            if (wordIndex >= _bits.Length) return -1;
            word = _bits[wordIndex];
        }
    }
}
