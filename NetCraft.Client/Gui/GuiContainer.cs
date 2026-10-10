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
namespace NetCraft.Client.Gui;

//GuiContainer a container that can hold child widgets
//Render by default walks visible children and calls their Render
//Event dispatch recurses into the child containing the point first, otherwise handles it itself
public abstract class GuiContainer : GuiControl
{
    private readonly List<GuiControl> _children = new();

    public IReadOnlyList<GuiControl> Children => _children;

    //Layout layout engine; null means no automatic layout and children use their own X/Y
    public IGuiLayout? Layout { get; set; }

    public void Add(GuiControl child)
    {
        if (child.Parent is not null)
            throw new InvalidOperationException("The widget already has a parent container");
        child.Parent = this;
        _children.Add(child);
        MarkDirty();
    }

    public bool Remove(GuiControl child)
    {
        if (!_children.Remove(child)) return false;
        child.Parent = null;
        MarkDirty();
        return true;
    }

    public void Clear()
    {
        foreach (var child in _children) child.Parent = null;
        _children.Clear();
        MarkDirty();
    }

    //ClearDirtyTree after a re-Render recording clears its own dirty flag and recurses into children
    //The whole subtree was re-rendered, so child dirty flags are cleared too to avoid a false positive next frame
    internal override void ClearDirtyTree()
    {
        _isDirty = false;
        foreach (var child in _children)
            child.ClearDirtyTree();
    }

    //MarkDirty override propagates up the parent chain then down to all children, invalidating caches
    //When parent container properties change (X/Y/Width/Height/Visible etc.) child pose/scissor snapshots go stale and need a re-Render
    protected override void MarkDirty()
    {
        base.MarkDirty();
        foreach (var child in _children)
            child.MarkDirtyDown();
    }

    //MarkDirtyDown propagates dirty down to children, recursing through sub-containers to the leaves
    //Already-dirty subtrees are skipped to avoid redundant marking
    internal override void MarkDirtyDown()
    {
        if (_isDirty) return;
        _isDirty = true;
        foreach (var c in _children)
            c.MarkDirtyDown();
    }

    public override void Render(IGuiRenderContext context)
    {
        if (!Visible) return;
        //Pushes the scissor to the container bounds to clip overflowing children; the GuiPanel background is drawn by the caller and is not clipped
        context.PushScissor(X, Y, Width, Height);
        foreach (var child in _children)
        {
            if (child.Visible) child.RenderWithCache(context);
        }
        context.PopScissor();
    }

    //Update calls Layout.Measure to arrange children, then walks visible children to Update animations
    public override void Update(double delta)
    {
        if (!Visible) return;
        Layout?.Measure(this);
        foreach (var child in _children)
        {
            if (child.Visible) child.Update(delta);
        }
    }

    //HitTest returns the topmost child containing the point, deepest level first
    public GuiControl? HitTest(int x, int y)
    {
        for (int i = _children.Count - 1; i >= 0; i--)
        {
            var child = _children[i];
            if (!child.Visible) continue;
            if (child is GuiContainer container)
            {
                var hit = container.HitTest(x, y);
                if (hit is not null) return hit;
            }
            else if (child.ContainsPoint(x, y))
            {
                return child;
            }
        }
        if (ContainsPoint(x, y)) return this;
        return null;
    }

    protected internal override void OnMouseDown(MouseEventArgs e)
    {
        var hit = HitTest(e.X, e.Y);
        if (hit is not null && hit != this)
        {
            hit.OnMouseDown(e);
        }
        else
        {
            base.OnMouseDown(e);
        }
    }

    protected internal override void OnMouseUp(MouseEventArgs e)
    {
        var hit = HitTest(e.X, e.Y);
        if (hit is not null && hit != this)
        {
            hit.OnMouseUp(e);
        }
        else
        {
            base.OnMouseUp(e);
        }
    }

    protected internal override void OnMouseMove(MouseEventArgs e)
    {
        var hit = HitTest(e.X, e.Y);
        if (hit is not null && hit != this)
        {
            hit.OnMouseMove(e);
        }
        else
        {
            base.OnMouseMove(e);
        }
    }

    public override void Dispose()
    {
        foreach (var child in _children) child.Dispose();
        _children.Clear();
        base.Dispose();
    }
}
