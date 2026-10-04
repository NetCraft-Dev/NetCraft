namespace NetCraft.Gpu;

//LayoutSettings 布局参数接口对标原版 LayoutSettings
//padding 四向独立 align 0~1 连续值链式 API
//0=左/上 0.5=中 1=右/下
//Java 允许字段方法同名 C# 不允许 Impl 字段加 Value 后缀避开冲突方法名保持对标原版
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

    //GetExposed 暴露可变内部 Impl 给布局引擎直接读写字段
    Impl GetExposed();

    //Defaults 默认配置 padding=0 align=0,0 左上对齐
    static LayoutSettings Defaults() => new Impl();

    //Impl 可变实现布局引擎直接读写此类的字段
    //对标原版 LayoutSettingsImpl paddingLeft/Top/Right/Bottom + xAlignment/yAlignment
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
