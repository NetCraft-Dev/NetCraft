using System.Numerics;

namespace NetCraft.Gpu;

//ScreenRectangle 屏幕矩形对标原版 ScreenRectangle
//用于 RenderState 的 ScissorArea 和 Bounds 层级相交判断
public readonly record struct ScreenRectangle(int X, int Y, int Width, int Height)
{
    public int Left => X;
    public int Top => Y;
    public int Right => X + Width;
    public int Bottom => Y + Height;

    //Intersects 判断两矩形是否相交对标原版 intersects
    public bool Intersects(ScreenRectangle other)
        => other.Left < Right && other.Right > Left && other.Top < Bottom && other.Bottom > Top;

    //Encompasses 判断本矩形是否完全包含 other 对标原版 encompasses
    public bool Encompasses(ScreenRectangle other)
        => other.Left >= Left && other.Top >= Top && other.Right <= Right && other.Bottom <= Bottom;

    //Contains Encompasses 别名保留旧调用方兼容
    public bool Contains(ScreenRectangle other) => Encompasses(other);

    //Intersect 求交集返回 null 表示不相交对标原版 intersection
    public ScreenRectangle? Intersect(ScreenRectangle other)
    {
        var left = Math.Max(Left, other.Left);
        var top = Math.Max(Top, other.Top);
        var right = Math.Min(Right, other.Right);
        var bottom = Math.Min(Bottom, other.Bottom);
        if (left >= right || top >= bottom) return null;
        return new ScreenRectangle(left, top, right - left, bottom - top);
    }

    //TransformMaxBounds 用 pose 变换四角取最大包围盒对标原版 transformMaxBounds
    //用于 RenderState 构造时由几何矩形 + pose 推导 Bounds
    public ScreenRectangle TransformMaxBounds(Matrix3x2 pose)
    {
        var topLeft = Vector2.Transform(new Vector2(Left, Top), pose);
        var topRight = Vector2.Transform(new Vector2(Right, Top), pose);
        var bottomLeft = Vector2.Transform(new Vector2(Left, Bottom), pose);
        var bottomRight = Vector2.Transform(new Vector2(Right, Bottom), pose);
        var minX = Math.Min(Math.Min(topLeft.X, bottomLeft.X), Math.Min(topRight.X, bottomRight.X));
        var maxX = Math.Max(Math.Max(topLeft.X, bottomLeft.X), Math.Max(topRight.X, bottomRight.X));
        var minY = Math.Min(Math.Min(topLeft.Y, bottomLeft.Y), Math.Min(topRight.Y, bottomRight.Y));
        var maxY = Math.Max(Math.Max(topLeft.Y, bottomLeft.Y), Math.Max(topRight.Y, bottomRight.Y));
        return new ScreenRectangle(
            (int)Math.Floor(minX),
            (int)Math.Floor(minY),
            (int)Math.Ceiling(maxX - minX),
            (int)Math.Ceiling(maxY - minY));
    }

    public static readonly ScreenRectangle Empty = default;

    //IsEmpty 宽高非正表示空矩形
    public bool IsEmpty => Width <= 0 || Height <= 0;
}
