namespace NetCraft.Gpu;

//GridLayout 网格布局对标原版 GridLayout extends AbstractLayout
//核心布局引擎按 row/column 定位子元素跨多行多列元素均分尺寸
//ArrangeElements 计算每列最大宽每行最大高再按 align 偏移定位
public sealed class GridLayout : AbstractLayout
{
    private readonly List<ChildContainer> _children = new();
    private LayoutSettings _defaultCellSettings = LayoutSettings.Defaults();
    private int _rowSpacing;
    private int _columnSpacing;

    public GridLayout() : this(0, 0) { }

    public GridLayout(int x, int y) : base(x, y, 0, 0) { }

    //ColumnSpacing/RowSpacing/Spacing 链式设置间距
    public GridLayout ColumnSpacing(int spacing) { _columnSpacing = spacing; return this; }
    public GridLayout RowSpacing(int spacing) { _rowSpacing = spacing; return this; }
    public GridLayout Spacing(int spacing) => ColumnSpacing(spacing).RowSpacing(spacing);

    //NewCellSettings/DefaultCellSetting 每个子元素默认 LayoutSettings 副本
    public LayoutSettings NewCellSettings() => _defaultCellSettings.Copy();
    public LayoutSettings DefaultCellSetting() => _defaultCellSettings;

    //AddChild 多重载对标原版默认 occupiedRows=1 occupiedColumns=1
    public T AddChild<T>(T child, int row, int column) where T : ILayoutElement
        => AddChild(child, row, column, 1, 1, NewCellSettings());

    public T AddChild<T>(T child, int row, int column, LayoutSettings settings) where T : ILayoutElement
        => AddChild(child, row, column, 1, 1, settings);

    public T AddChild<T>(T child, int row, int column, int rows, int columns) where T : ILayoutElement
        => AddChild(child, row, column, rows, columns, NewCellSettings());

    //AddChild 核心重载指定 row/column/occupiedRows/occupiedColumns + cellSettings
    public T AddChild<T>(T child, int row, int column, int rows, int columns, LayoutSettings cellSettings) where T : ILayoutElement
    {
        if (rows < 1) throw new ArgumentException("Occupied rows must be at least 1");
        if (columns < 1) throw new ArgumentException("Occupied columns must be at least 1");
        _children.Add(new ChildContainer(child, row, column, rows, columns, cellSettings));
        return child;
    }

    //CreateRowHelper 创建行辅助器按 columns 列自动换行添加子元素
    public RowHelper CreateRowHelper(int columns) => new(this, columns);

    //ArrangeElements 计算每列最大宽每行最大高按 align 偏移定位子元素
    //跨多行多列的元素高度/宽度减去间距后用 Divisor 均分到各行列
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
            //跨多行的元素高度减去行间距后均分到各行
            int childHeight = c.GetHeight() - (c.OccupiedRows - 1) * _rowSpacing;
            var heightDiv = new Divisor(childHeight, c.OccupiedRows);
            for (int row = c.Row; row <= c.GetLastOccupiedRow(); row++)
                maxRowHeights[row] = Math.Max(maxRowHeights[row], heightDiv.NextInt());

            //跨多列的元素宽度减去列间距后均分到各列
            int childWidth = c.GetWidth() - (c.OccupiedColumns - 1) * _columnSpacing;
            var widthDiv = new Divisor(childWidth, c.OccupiedColumns);
            for (int col = c.Column; col <= c.GetLastOccupiedColumn(); col++)
                maxColumnWidths[col] = Math.Max(maxColumnWidths[col], widthDiv.NextInt());
        }

        //累加列 X 偏移和行 Y 偏移含间距
        var columnXOffsets = new int[maxColumn + 1];
        var rowYOffsets = new int[maxRow + 1];
        for (int col = 1; col <= maxColumn; col++)
            columnXOffsets[col] = columnXOffsets[col - 1] + maxColumnWidths[col - 1] + _columnSpacing;
        for (int row = 1; row <= maxRow; row++)
            rowYOffsets[row] = rowYOffsets[row - 1] + maxRowHeights[row - 1] + _rowSpacing;

        //每个子元素按 align 偏移定位 availableSpace=跨列宽度之和+间距
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

    //ChildContainer 网格子元素容器含 row/column/occupiedRows/occupiedColumns
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

    //RowHelper 行辅助器按 columns 列自动换行添加子元素
    //对标原版 GridLayout.RowHelper index 超过列数自动换行
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

        //AddChild 自动算 row/column 超过列数换行 occupiedColumns 跨多列时不足换行
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

        //RoundToward 向上取整到最近的 columns 倍数对标原版 Mth.roundToward
        private static int RoundToward(int value, int divisor)
            => ((value + divisor - 1) / divisor) * divisor;
    }
}
