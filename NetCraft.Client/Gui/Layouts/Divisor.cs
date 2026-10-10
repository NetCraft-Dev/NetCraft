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
namespace NetCraft.Client.Gui.Layouts;

//Divisor division helper, maps to vanilla com.mojang.math.Divisor
//Splits total into parts shares with the remainder going to the first few; each NextInt returns one share
//Used by GridLayout to divide the height/width of multi-row/column elements across rows and columns
public struct Divisor
{
    private readonly int _total;
    private readonly int _parts;
    private int _given;

    public Divisor(int total, int parts)
    {
        _total = total;
        _parts = parts;
        _given = 0;
    }

    //NextInt returns the next share; baseShare=total/parts with the remainder spread over the first remainder shares
    //The sum over all parts calls equals total
    public int NextInt()
    {
        if (_given >= _parts) return 0;
        int baseShare = _total / _parts;
        int remainder = _total % _parts;
        int result = baseShare + (_given < remainder ? 1 : 0);
        _given++;
        return result;
    }
}
