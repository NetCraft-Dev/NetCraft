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
namespace NetCraft.Client.Gui.Font;

//FontOption font option, maps to vanilla FontOption
//The uniform/alt/illageralt filters match providers against the active options
public sealed class FontOption
{
    public string Name { get; }
    public static readonly FontOption Uniform = new("uniform");
    public static readonly FontOption Alt = new("alt");
    public static readonly FontOption IllagerAlt = new("illageralt");

    private FontOption(string name) { Name = name; }

    public override string ToString() => Name;
    public override int GetHashCode() => Name.GetHashCode(StringComparison.Ordinal);
    public override bool Equals(object? obj) => obj is FontOption o && o.Name == Name;
}

//FontOptionFilter filter condition, maps to vanilla FontOption.Filter
//Holds the FontOption→required mapping; Apply checks whether the options set satisfies all conditions
//The filter:{uniform:false} in default.json matches when the uniform option is not active
public sealed class FontOptionFilter
{
    private readonly Dictionary<FontOption, bool> _conditions;
    public static readonly FontOptionFilter AlwaysPass = new(new Dictionary<FontOption, bool>());

    public FontOptionFilter(Dictionary<FontOption, bool> conditions) => _conditions = conditions;

    //Apply returns true when the options set satisfies all conditions; an empty condition always passes
    public bool Apply(IReadOnlySet<FontOption> options)
    {
        foreach (var (option, required) in _conditions)
            if (options.Contains(option) != required) return false;
        return true;
    }
}
