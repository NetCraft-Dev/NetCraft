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
namespace NetCraft.Client.Render.State.Gui;

//GuiRenderState RenderState container, maps to vanilla GuiRenderState
//Manages strata horizontal layering and up vertical levels to guarantee z order
//Reset every frame, not cross-frame
public sealed class GuiRenderState
{
    private Node? _current;
    private ScreenRectangle? _lastElementBounds;
    private readonly List<Node> _strata = new();
    private int _firstStratumAfterBlur = int.MaxValue;

    public GuiRenderState() : this(skipInit: false) { }

    //Snapshot deep-copies the strata+Node tree for the Render thread to read
    //Element records are immutable and shared by reference; only the List containers and Node structure are copied
    private GuiRenderState(bool skipInit)
    {
        if (!skipInit) NextStratum();
    }

    //Snapshot produces an immutable snapshot, called after Tick writes and read-only on the Render thread
    public GuiRenderState Snapshot()
    {
        var copy = new GuiRenderState(skipInit: true);
        foreach (var node in _strata) copy._strata.Add(CloneNode(node));
        copy._firstStratumAfterBlur = _firstStratumAfterBlur;
        return copy;
    }

    //CloneNode rebuilds the Node's Up chain and ElementStates/GlyphStates/PipStates lists
    private static Node CloneNode(Node src)
    {
        var dst = new Node(src.Parent);
        if (src.Up is not null) dst.Up = CloneNode(src.Up);
        if (src.ElementStates is not null)
            dst.ElementStates = new List<GuiElementRenderState>(src.ElementStates);
        if (src.GlyphStates is not null)
            dst.GlyphStates = new List<GuiElementRenderState>(src.GlyphStates);
        if (src.PipStates is not null)
            dst.PipStates = new List<PictureInPictureRenderState>(src.PipStates);
        return dst;
    }

    //NextStratum opens a new stratum for horizontal layering: background/content/overlay is typical
    public void NextStratum()
    {
        _current = new Node(null);
        _strata.Add(_current);
    }

    //HasBlurSplit whether BlurBeforeThisStratum was called, requiring segmented Prepare+Draw execution
    public bool HasBlurSplit => _firstStratumAfterBlur != int.MaxValue;

    //BlurBeforeThisStratum marks everything before the current stratum as the pre-blur segment; callable once per frame
    public void BlurBeforeThisStratum()
    {
        if (_firstStratumAfterBlur != int.MaxValue)
            throw new InvalidOperationException("Can only blur once per frame");
        _firstStratumAfterBlur = _strata.Count - 1;
    }

    //Up creates a child node above the current Node and switches current to it, ensuring later elements draw on top
    public void Up()
    {
        if (_current!.Up == null)
            _current.Up = new Node(_current);
        _current = _current.Up;
    }

    //AddGuiElement adds an ordinary element, auto-positioned by FindAppropriateNode
    public void AddGuiElement(GuiElementRenderState element)
    {
        FindAppropriateNode(element);
        _current!.AddElement(element);
    }

    //AddGlyphToCurrentLayer adds glyphs directly to the current layer, not participating in the bounds tree
    //Glyph bounds come from the glyph texture itself and do not participate in level intersection tests
    public void AddGlyphToCurrentLayer(GuiElementRenderState glyph)
    {
        _current!.AddGlyph(glyph);
    }

    //AddPictureInPicture adds PIP state to the current layer, maps to vanilla addPicturesInPictureState
    //PIP does not participate in bounds tree level tests; PictureInPictureRenderer renders offscreen and blits during Prepare
    public void AddPictureInPicture(PictureInPictureRenderState pip)
    {
        _current!.AddPip(pip);
    }

    //ForEachPictureInPicture iterates pipStates of all strata, maps to vanilla forEachPictureInPicture
    //PIP does not participate in blur segmentation; it iterates all strata and calls PictureInPictureRenderer.prepare during Prepare
    public void ForEachPictureInPicture(Action<PictureInPictureRenderState> visitor)
    {
        foreach (var node in _strata)
            TraversePip(node, visitor);
    }

    private static void TraversePip(Node node, Action<PictureInPictureRenderState> visitor)
    {
        if (node.PipStates != null)
            foreach (var pip in node.PipStates) visitor(pip);
        if (node.Up != null) TraversePip(node.Up, visitor);
    }

    //ForEachElement iterates elements by range, depth-first over elementStates + glyphStates
    public void ForEachElement(Action<GuiElementRenderState> visitor, TraverseRange range)
    {
        Traverse(node =>
        {
            if (node.ElementStates == null && node.GlyphStates == null) return;
            if (node.ElementStates != null)
                foreach (var e in node.ElementStates) visitor(e);
            if (node.GlyphStates != null)
                foreach (var g in node.GlyphStates) visitor(g);
        }, range);
    }

    //SortElements sorts each Node's elementStates by the comparator; glyphStates do not participate
    public void SortElements(Comparison<GuiElementRenderState> comparison)
    {
        Traverse(node =>
        {
            if (node.ElementStates != null)
                node.ElementStates.Sort(comparison);
        }, TraverseRange.All);
    }

    //Reset clears all state and opens the first stratum, called every frame
    public void Reset()
    {
        _strata.Clear();
        _firstStratumAfterBlur = int.MaxValue;
        _lastElementBounds = null;
        NextStratum();
    }

    //FindAppropriateNode finds the Node where the element should be inserted
    //Bounds are non-nullable and always continue; if lastElementBounds contains the current bounds go Up, otherwise search upward for an intersecting node
    private void FindAppropriateNode(GuiElementRenderState element)
    {
        var bounds = element.Bounds;
        if (_lastElementBounds is { } lastBounds && lastBounds.Encompasses(bounds))
        {
            Up();
        }
        else
        {
            NavigateToAboveHighestElementWithIntersectingBounds(bounds);
        }
        _lastElementBounds = bounds;
    }

    //NavigateToAboveHighestElementWithIntersectingBounds searches up from the strata top for the highest intersecting node
    //If found, current = after the intersecting node and Up adds a new layer above it; if not found, current = root
    private void NavigateToAboveHighestElementWithIntersectingBounds(ScreenRectangle bounds)
    {
        var node = _strata[^1];
        while (node.Up != null)
            node = node.Up;

        bool found = false;
        while (!found)
        {
            found = HasIntersection(bounds, node.ElementStates)
                || HasIntersection(bounds, node.GlyphStates);
            if (node.Parent == null) break;
            if (!found) node = node.Parent;
        }

        _current = node;
        if (found) Up();
    }

    private static bool HasIntersection(ScreenRectangle bounds, List<GuiElementRenderState>? states)
    {
        if (states == null) return false;
        foreach (var s in states)
            if (s.Bounds.Intersects(bounds)) return true;
        return false;
    }

    private void Traverse(Action<Node> visitor, TraverseRange range)
    {
        int start = 0;
        int end = _strata.Count;
        if (range == TraverseRange.BeforeBlur)
            end = Math.Min(_firstStratumAfterBlur, _strata.Count);
        else if (range == TraverseRange.AfterBlur)
            start = _firstStratumAfterBlur;

        for (int i = start; i < end; i++)
            Traverse(_strata[i], visitor);
    }

    private static void Traverse(Node node, Action<Node> visitor)
    {
        visitor(node);
        if (node.Up != null) Traverse(node.Up, visitor);
    }

    //Node inner level node with lazily initialized state lists to avoid empty-container memory cost
    private sealed class Node
    {
        public readonly Node? Parent;
        public Node? Up;
        public List<GuiElementRenderState>? ElementStates;
        public List<GuiElementRenderState>? GlyphStates;
        //PipStates PIP render state list lazily initialized, maps to vanilla picturesInPictureStates
        public List<PictureInPictureRenderState>? PipStates;

        public Node(Node? parent) => Parent = parent;

        public void AddElement(GuiElementRenderState element)
        {
            ElementStates ??= new List<GuiElementRenderState>();
            ElementStates.Add(element);
        }

        public void AddGlyph(GuiElementRenderState glyph)
        {
            GlyphStates ??= new List<GuiElementRenderState>();
            GlyphStates.Add(glyph);
        }

        public void AddPip(PictureInPictureRenderState pip)
        {
            PipStates ??= new List<PictureInPictureRenderState>();
            PipStates.Add(pip);
        }
    }
}
