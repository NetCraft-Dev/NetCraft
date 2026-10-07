namespace NetCraft.Gpu;

//LayoutSettings layout params interface, maps to vanilla LayoutSettings
//padding per side, align a continuous 0~1 value, chained API
//0=left/top 0.5=center 1=right/bottom
//Java allows a field and method with the same name, C# does not; the Impl fields take a Value suffix to avoid clashing with the method names while keeping parity with vanilla
public interface LayoutSettings
{
    LayoutSettings Padding(int padding);
    LayoutSettings Padding(int horizontal, int vertical);
    LayoutSettings Padding(int left, int top, int right, int bottom);
    LayoutSettings PaddingLeft(int padding);
    LayoutSettings PaddingTop(int padding);
    LayoutSettings PaddingRight(int padding);
    LayoutSettings PaddingBottom(int padding);
    LayoutSettings PaddingHorizontal(int padding);
    LayoutSettings PaddingVertical(int padding);
    LayoutSettings Align(float xAlignment, float yAlignment);
    LayoutSettings AlignHorizontally(float xAlignment);
    LayoutSettings AlignVertically(float yAlignment);
    LayoutSettings Copy();

    //GetExposed exposes the mutable inner Impl so the layout engine reads/writes the fields directly
    Impl GetExposed();

    //Defaults default config padding=0 align=0,0 top-left aligned
    static LayoutSettings Defaults() => new Impl();

    //Impl mutable implementation; the layout engine reads/writes this class's fields directly
    //maps to vanilla LayoutSettingsImpl paddingLeft/Top/Right/Bottom + xAlignment/yAlignment
    public sealed class Impl : LayoutSettings
    {
        public int PaddingLeftValue;
        public int PaddingTopValue;
        public int PaddingRightValue;
        public int PaddingBottomValue;
        public float XAlignment;
        public float YAlignment;

        public Impl() { }

        public Impl(Impl copy)
        {
            PaddingLeftValue = copy.PaddingLeftValue;
            PaddingTopValue = copy.PaddingTopValue;
            PaddingRightValue = copy.PaddingRightValue;
            PaddingBottomValue = copy.PaddingBottomValue;
            XAlignment = copy.XAlignment;
            YAlignment = copy.YAlignment;
        }

        public LayoutSettings Padding(int padding) => Padding(padding, padding);
        public LayoutSettings Padding(int horizontal, int vertical)
            => PaddingHorizontal(horizontal).PaddingVertical(vertical);
        public LayoutSettings Padding(int left, int top, int right, int bottom)
            => PaddingLeft(left).PaddingRight(right).PaddingTop(top).PaddingBottom(bottom);

        public LayoutSettings PaddingLeft(int padding) { PaddingLeftValue = padding; return this; }
        public LayoutSettings PaddingTop(int padding) { PaddingTopValue = padding; return this; }
        public LayoutSettings PaddingRight(int padding) { PaddingRightValue = padding; return this; }
        public LayoutSettings PaddingBottom(int padding) { PaddingBottomValue = padding; return this; }
        public LayoutSettings PaddingHorizontal(int padding)
            => PaddingLeft(padding).PaddingRight(padding);
        public LayoutSettings PaddingVertical(int padding)
            => PaddingTop(padding).PaddingBottom(padding);

        public LayoutSettings Align(float xAlignment, float yAlignment)
        { XAlignment = xAlignment; YAlignment = yAlignment; return this; }
        public LayoutSettings AlignHorizontally(float xAlignment)
        { XAlignment = xAlignment; return this; }
        public LayoutSettings AlignVertically(float yAlignment)
        { YAlignment = yAlignment; return this; }

        public LayoutSettings Copy() => new Impl(this);
        public Impl GetExposed() => this;
    }
}
