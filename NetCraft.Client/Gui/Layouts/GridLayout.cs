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

//GridLayout grid layout, maps to vanilla GridLayout extends AbstractLayout
//Core layout engine positioning children by row/column, dividing size for multi-row/column elements
//ArrangeElements computes each column's max width and each row's max height, then offsets positions by align
public sealed class GridLayout : AbstractLayout
{
    private readonly List<ChildContainer> _children = new();
    private LayoutSettings _defaultCellSettings = LayoutSettings.Defaults();
    private int _rowSpacing;
    private int _columnSpacing;

    public GridLayout() : this(0, 0) { }

    public GridLayout(int x, int y) : base(x, y, 0, 0) { }

    //ColumnSpacing/RowSpacing/Spacing chained spacing setters
    public GridLayout ColumnSpacing(int spacing) { _columnSpacing = spacing; return this; }
    public GridLayout RowSpacing(int spacing) { _rowSpacing = spacing; return this; }
    public GridLayout Spacing(int spacing) => ColumnSpacing(spacing).RowSpacing(spacing);

    //NewCellSettings/DefaultCellSetting a default LayoutSettings copy per child
    public LayoutSettings NewCellSettings() => _defaultCellSettings.Copy();
    public LayoutSettings DefaultCellSetting() => _defaultCellSettings;

    //AddChild overloads, maps to vanilla defaulting occupiedRows=1 occupiedColumns=1
    public T AddChild<T>(T child, int row, int column) where T : ILayoutElement
        => AddChild(child, row, column, 1, 1, NewCellSettings());

    public T AddChild<T>(T child, int row, int column, LayoutSettings settings) where T : ILayoutElement
        => AddChild(child, row, column, 1, 1, settings);

    public T AddChild<T>(T child, int row, int column, int rows, int columns) where T : ILayoutElement
        => AddChild(child, row, column, rows, columns, NewCellSettings());

    //AddChild core overload specifying row/column/occupiedRows/occupiedColumns + cellSettings
    public T AddChild<T>(T child, int row, int column, int rows, int columns, LayoutSettings cellSettings) where T : ILayoutElement
    {
        if (rows < 1) throw new ArgumentException("Occupied rows must be at least 1");
        if (columns < 1) throw new ArgumentException("Occupied columns must be at least 1");
        _children.Add(new ChildContainer(child, row, column, rows, columns, cellSettings));
        return child;
    }

    //CreateRowHelper creates a row helper that adds children with automatic wrapping at columns
    public RowHelper CreateRowHelper(int columns) => new(this, columns);

    //ArrangeElements computes each column's max width and each row's max height then positions children by align
    //Multi-row/column elements have spacing subtracted then use a Divisor to split across rows and columns
    public override void ArrangeElements()
    {
        base.ArrangeElements();
        if (_children.Count == 0)
        {
            _width = 0;
            _height = 0;
            return;
        }

        int maxRow = 0, maxColumn = 0;
        foreach (var c in _children)
        {
            maxRow = Math.Max(c.GetLastOccupiedRow(), maxRow);
            maxColumn = Math.Max(c.GetLastOccupiedColumn(), maxColumn);
        }

        var maxColumnWidths = new int[maxColumn + 1];
        var maxRowHeights = new int[maxRow + 1];
        foreach (var c in _children)
        {
            //A multi-row element's height minus row spacing is divided across rows
            int childHeight = c.GetHeight() - (c.OccupiedRows - 1) * _rowSpacing;
            var heightDiv = new Divisor(childHeight, c.OccupiedRows);
            for (int row = c.Row; row <= c.GetLastOccupiedRow(); row++)
                maxRowHeights[row] = Math.Max(maxRowHeights[row], heightDiv.NextInt());

            //A multi-column element's width minus column spacing is divided across columns
            int childWidth = c.GetWidth() - (c.OccupiedColumns - 1) * _columnSpacing;
            var widthDiv = new Divisor(childWidth, c.OccupiedColumns);
            for (int col = c.Column; col <= c.GetLastOccupiedColumn(); col++)
                maxColumnWidths[col] = Math.Max(maxColumnWidths[col], widthDiv.NextInt());
        }

        //Accumulates the column X offset and row Y offset including spacing
        var columnXOffsets = new int[maxColumn + 1];
        var rowYOffsets = new int[maxRow + 1];
        for (int col = 1; col <= maxColumn; col++)
            columnXOffsets[col] = columnXOffsets[col - 1] + maxColumnWidths[col - 1] + _columnSpacing;
        for (int row = 1; row <= maxRow; row++)
            rowYOffsets[row] = rowYOffsets[row - 1] + maxRowHeights[row - 1] + _rowSpacing;

        //Each child is offset by align; availableSpace=sum of spanned column widths+spacing
        foreach (var c in _children)
        {
            int availableWidth = 0;
            for (int col = c.Column; col <= c.GetLastOccupiedColumn(); col++)
                availableWidth += maxColumnWidths[col];
            c.SetX(X + columnXOffsets[c.Column], availableWidth + _columnSpacing * (c.OccupiedColumns - 1));

            int availableHeight = 0;
            for (int row = c.Row; row <= c.GetLastOccupiedRow(); row++)
                availableHeight += maxRowHeights[row];
            c.SetY(Y + rowYOffsets[c.Row], availableHeight + _rowSpacing * (c.OccupiedRows - 1));
        }

        _width = columnXOffsets[maxColumn] + maxColumnWidths[maxColumn];
        _height = rowYOffsets[maxRow] + maxRowHeights[maxRow];
    }

    public override void VisitChildren(Action<ILayoutElement> visitor)
    {
        foreach (var c in _children) visitor(c.Child);
    }

    public override void RemoveChildren() => _children.Clear();

    //ChildContainer grid child container with row/column/occupiedRows/occupiedColumns
    private sealed class ChildContainer : ChildWrapper
    {
        public readonly int Row;
        public readonly int Column;
        public readonly int OccupiedRows;
        public readonly int OccupiedColumns;

        public ChildContainer(ILayoutElement child, int row, int column, int occupiedRows, int occupiedColumns, LayoutSettings cellSettings)
            : base(child, cellSettings)
        {
            Row = row;
            Column = column;
            OccupiedRows = occupiedRows;
            OccupiedColumns = occupiedColumns;
        }

        public int GetLastOccupiedRow() => Row + OccupiedRows - 1;
        public int GetLastOccupiedColumn() => Column + OccupiedColumns - 1;
    }

    //RowHelper row helper adding children with automatic wrapping at columns
    //maps to vanilla GridLayout.RowHelper: wraps automatically when the index exceeds the column count
    public sealed class RowHelper
    {
        private readonly GridLayout _grid;
        private readonly int _columns;
        private int _index;

        internal RowHelper(GridLayout grid, int columns)
        {
            _grid = grid;
            _columns = columns;
        }

        public T AddChild<T>(T child) where T : ILayoutElement
            => AddChild(child, 1);

        public T AddChild<T>(T child, int occupiedColumns) where T : ILayoutElement
            => AddChild(child, occupiedColumns, _grid.NewCellSettings());

        public T AddChild<T>(T child, LayoutSettings settings) where T : ILayoutElement
            => AddChild(child, 1, settings);

        //AddChild computes row/column automatically, wrapping past the column count; with occupiedColumns spanning multiple columns it wraps when short
        public T AddChild<T>(T child, int occupiedColumns, LayoutSettings settings) where T : ILayoutElement
        {
            int row = _index / _columns;
            int col = _index % _columns;
            if (col + occupiedColumns > _columns)
            {
                row++;
                col = 0;
                _index = RoundToward(_index, _columns);
            }
            _index += occupiedColumns;
            return _grid.AddChild(child, row, col, 1, occupiedColumns, settings);
        }

        public LayoutSettings NewCellSettings() => _grid.NewCellSettings();
        public LayoutSettings DefaultCellSetting() => _grid.DefaultCellSetting();

        //RoundToward rounds up to the nearest multiple of columns, maps to vanilla Mth.roundToward
        private static int RoundToward(int value, int divisor)
            => ((value + divisor - 1) / divisor) * divisor;
    }
}
