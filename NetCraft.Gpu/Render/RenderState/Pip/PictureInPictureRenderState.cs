using System.Numerics;

namespace NetCraft.Gpu;

//PictureInPictureRenderState PIP 渲染状态接口对标原版 pip.PictureInPictureRenderState
//GUI 嵌 3D 内容（玩家皮肤/旗帜/实体预览）的渲染状态值对象
//submission 阶段由 Screen 构造提交到 GuiRenderState.AddPictureInPicture
//render 阶段由 PictureInPictureRenderer<T> 子类 offscreen 渲染 3D 内容后 blit 到 GUI
public interface PictureInPictureRenderState
{
    //X0/Y0/X1/Y1 PIP 区域在 GUI 坐标系的矩形左上右下对标原版 x0/y0/x1/y1
    int X0 { get; }
    int Y0 { get; }
    int X1 { get; }
    int Y1 { get; }

    //Scale 3D 内容缩放倍率对标原版 scale 由子类按模型尺寸设定
    float Scale { get; }

    //ScissorArea 裁剪矩形空表示不裁剪
    ScreenRectangle ScissorArea { get; }

    //Pose 2D 变换矩阵默认单位矩阵对标原版 pose() default IDENTITY_POSE
    //PIP 区域通常无 pose 变换 blit 时直接用 X0/Y0/X1/Y1
    Matrix3x2 Pose { get; }

    //Bounds 用于层级相交判断由几何矩形与 scissor 求交得出
    ScreenRectangle Bounds { get; }

    //GetBounds 由几何矩形与 scissor 求交计算 Bounds 对标原版 getBounds
    //scissor 为空时直接用几何矩形否则取交集
    public static ScreenRectangle GetBounds(int x0, int y0, int x1, int y1, ScreenRectangle scissorArea)
    {
        var raw = new ScreenRectangle(x0, y0, x1 - x0, y1 - y0);
        return scissorArea.IsEmpty ? raw : scissorArea.Intersect(raw) ?? raw;
    }
}
