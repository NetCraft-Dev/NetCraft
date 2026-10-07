using System.Numerics;

namespace NetCraft.Gpu;

//ScreenRectangle screen rectangle, maps to vanilla ScreenRectangle
//Used for RenderState's ScissorArea and Bounds level intersection tests
public readonly record struct ScreenRectangle(int X, int Y, int Width, int Height)
{
    public int Left => X;
    public int Top => Y;
    public int Right => X + Width;
    public int Bottom => Y + Height;

    //Intersects whether two rectangles intersect, maps to vanilla intersects
    public bool Intersects(ScreenRectangle other)
        => other.Left < Right && other.Right > Left && other.Top < Bottom && other.Bottom > Top;

    //Encompasses whether this rectangle fully contains other, maps to vanilla encompasses
    public bool Encompasses(ScreenRectangle other)
        => other.Left >= Left && other.Top >= Top && other.Right <= Right && other.Bottom <= Bottom;

    //Contains an alias of Encompasses kept for legacy callers
    public bool Contains(ScreenRectangle other) => Encompasses(other);

    //Intersect computes the intersection and returns null when disjoint, maps to vanilla intersection
    public ScreenRectangle? Intersect(ScreenRectangle other)
    {
        var left = Math.Max(Left, other.Left);
        var top = Math.Max(Top, other.Top);
        var right = Math.Min(Right, other.Right);
        var bottom = Math.Min(Bottom, other.Bottom);
        if (left >= right || top >= bottom) return null;
        return new ScreenRectangle(left, top, right - left, bottom - top);
    }

    //TransformMaxBounds transforms the four corners with the pose and takes the max bounding box, maps to vanilla transformMaxBounds
    //Used to derive Bounds from the geometry rectangle + pose when constructing a RenderState
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

    //IsEmpty a non-positive width or height means an empty rectangle
    public bool IsEmpty => Width <= 0 || Height <= 0;
}
