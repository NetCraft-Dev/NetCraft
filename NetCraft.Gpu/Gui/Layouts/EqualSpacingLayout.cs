namespace NetCraft.Gpu;

//EqualSpacingLayout equal-spacing layout, maps to vanilla EqualSpacingLayout extends AbstractLayout
//Children are equally spaced along the main axis and aligned to the start on the cross axis
//A Divisor splits remainingSpace across size-1 gaps on the main axis
public sealed class EqualSpacingLayout : AbstractLayout
{
    private readonly Orientation _orientation;
    private readonly List<ChildContainer> _children = new();
    private LayoutSettings _defaultChildSettings;

    public EqualSpacingLayout(int width, int height, Orientation orientation) : this(0, 0, width, height, orientation) { }

    public EqualSpacingLayout(int x, int y, int width, int height, Orientation orientation) : base(x, y, width, height)
    {
        _defaultChildSettings = LayoutSettings.Defaults();
        _orientation = orientation;
    }

    public LayoutSettings NewChildLayoutSettings() => _defaultChildSettings.Copy();
    public LayoutSettings DefaultChildLayoutSetting() => _defaultChildSettings;

    //ArrangeElements equally spaces children on the main axis and aligns them to the start on the cross axis
    //The first child goes to the main-axis start; later children are spread by the divided gaps and aligned on the cross axis
    public override void ArrangeElements()
    {
        base.ArrangeElements();
        if (_children.Count == 0) return;

        int totalPrimary = 0;
        //The cross-axis length starts from the layout's own and takes the max with the children
        int maxSecondary = GetSecondaryLength(this);
        foreach (var c in _children)
        {
            totalPrimary += GetPrimaryLength(c);
            maxSecondary = Math.Max(maxSecondary, GetSecondaryLength(c));
        }

        int remaining = GetPrimaryLength(this) - totalPrimary;
        int position = GetPrimaryPosition(this);

        var first = _children[0];
        SetPrimaryPosition(first, position);
        int nextPos = position + GetPrimaryLength(first);

        //With size>=2, remaining is split across size-1 gaps driving later children's main-axis positions
        if (_children.Count >= 2)
        {
            var divisor = new Divisor(remaining, _children.Count - 1);
            for (int i = 1; i < _children.Count; i++)
            {
                int p = nextPos + divisor.NextInt();
                var c = _children[i];
                SetPrimaryPosition(c, p);
                nextPos = p + GetPrimaryLength(c);
            }
        }

        int secondaryPos = GetSecondaryPosition(this);
        foreach (var c in _children)
            SetSecondaryPosition(c, secondaryPos, maxSecondary);

        //The cross-axis size converges to the children's max and the main-axis size keeps its constructed value
        if (_orientation == Orientation.Horizontal)
            _height = maxSecondary;
        else
            _width = maxSecondary;
    }

    public T AddChild<T>(T child) where T : ILayoutElement
        => AddChild(child, NewChildLayoutSettings());

    public T AddChild<T>(T child, LayoutSettings settings) where T : ILayoutElement
    {
        _children.Add(new ChildContainer(child, settings));
        return child;
    }

    public override void VisitChildren(Action<ILayoutElement> visitor)
    {
        foreach (var c in _children) visitor(c.Child);
    }

    public override void RemoveChildren() => _children.Clear();

    //Orientation direction Horizontal horizontal main axis Vertical vertical main axis
    public enum Orientation
    {
        Horizontal,
        Vertical
    }

    //Main/cross-axis length and coordinate access are dispatched by _orientation
    //ChildContainer's GetWidth/GetHeight include padding, used by layout computation
    private int GetPrimaryLength(ChildContainer c) => _orientation == Orientation.Horizontal ? c.GetWidth() : c.GetHeight();
    private int GetSecondaryLength(ChildContainer c) => _orientation == Orientation.Horizontal ? c.GetHeight() : c.GetWidth();
    private int GetPrimaryLength(ILayoutElement e) => _orientation == Orientation.Horizontal ? e.Width : e.Height;
    private int GetSecondaryLength(ILayoutElement e) => _orientation == Orientation.Horizontal ? e.Height : e.Width;
    private int GetPrimaryPosition(ILayoutElement e) => _orientation == Orientation.Horizontal ? e.X : e.Y;
    private int GetSecondaryPosition(ILayoutElement e) => _orientation == Orientation.Horizontal ? e.Y : e.X;

    //SetPrimaryPosition the main-axis position is already determined by equal spacing; availableSpace=own length so align only handles padding adjustment
    private void SetPrimaryPosition(ChildContainer c, int pos)
    {
        if (_orientation == Orientation.Horizontal) c.SetX(pos, c.GetWidth());
        else c.SetY(pos, c.GetHeight());
    }

    //SetSecondaryPosition aligns by align within availableSpace on the cross axis
    private void SetSecondaryPosition(ChildContainer c, int pos, int availableSpace)
    {
        if (_orientation == Orientation.Horizontal) c.SetY(pos, availableSpace);
        else c.SetX(pos, availableSpace);
    }

    private sealed class ChildContainer : ChildWrapper
    {
        public ChildContainer(ILayoutElement child, LayoutSettings settings) : base(child, settings) { }
    }
}
