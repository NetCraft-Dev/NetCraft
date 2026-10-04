using System.Numerics;

namespace NetCraft.Gpu;

//PoseStack 矩阵栈对标原版 com.mojang.blaze3d.vertex.PoseStack
//3D 物品渲染链路用 translate/scale 把物品放到图集槽位 item.submit 时 pushPose 快照变换
//法线矩阵从 pose 逆转置取 3x3 部分原版有 trustedNormals 优化当前简化为按需重算
public sealed class PoseStack
{
    private readonly Stack<Matrix4x4> _poses = new();
    //法线矩阵缓存 dirty 时从栈顶 pose 重算
    private Matrix4x4 _normal = Matrix4x4.Identity;
    private bool _normalDirty = true;

    public PoseStack()
    {
        _poses.Push(Matrix4x4.Identity);
    }

    //Pose 取栈顶模型矩阵法线矩阵按需重算
    public Matrix4x4 Pose()
    {
        _normalDirty = true;
        return _poses.Peek();
    }

    //Normal 取栈顶法线矩阵 pose 的逆转置 3x3 部分
    public Matrix4x4 Normal()
    {
        if (_normalDirty)
        {
            _normal = ComputeNormalMatrix(_poses.Peek());
            _normalDirty = false;
        }
        return _normal;
    }

    public void PushPose() => _poses.Push(_poses.Peek());
    public void PopPose() => _poses.Pop();
    public bool IsEmpty => _poses.Count == 1;

    //Translate 平移栈顶矩阵
    public void Translate(float x, float y, float z)
    {
        var top = _poses.Pop();
        top *= Matrix4x4.CreateTranslation(x, y, z);
        _poses.Push(top);
        _normalDirty = true;
    }

    //Scale 缩放栈顶矩阵 GuiItemAtlas 用 scale(size,-size,size) 翻转 Y
    public void Scale(float x, float y, float z)
    {
        var top = _poses.Pop();
        top *= Matrix4x4.CreateScale(x, y, z);
        _poses.Push(top);
        _normalDirty = true;
    }

    //MulPose 右乘任意矩阵用于 itemTransform 应用
    public void MulPose(Matrix4x4 matrix)
    {
        var top = _poses.Pop();
        top *= matrix;
        _poses.Push(top);
        _normalDirty = true;
    }

    //Rotate 用四元数旋转栈顶
    public void Rotate(Quaternion rotation)
    {
        var top = _poses.Pop();
        top *= Matrix4x4.CreateFromQuaternion(rotation);
        _poses.Push(top);
        _normalDirty = true;
    }

    public void SetIdentity()
    {
        _poses.Clear();
        _poses.Push(Matrix4x4.Identity);
        _normal = Matrix4x4.Identity;
        _normalDirty = false;
    }

    //TransformPosition 用栈顶 pose 变换顶点位置到目标空间
    public Vector3 TransformPosition(float x, float y, float z)
        => Vector3.Transform(new Vector3(x, y, z), _poses.Peek());

    //TransformNormal 用法线矩阵变换法线
    public Vector3 TransformNormal(float x, float y, float z)
        => Vector3.Normalize(Vector3.TransformNormal(new Vector3(x, y, z), Normal()));

    //Copy 快照栈顶 pose 供 submit 延迟渲染后续 execute 时 poseStack 已 pop 也能绘制
    public Matrix4x4 Copy() => _poses.Peek();

    //ComputeNormalMatrix 从 pose 矩阵计算法线矩阵逆转置取 3x3 部分
    //System.Numerics 无 Matrix3x3 用 Matrix4x4 的逆转置取上 3x3
    private static Matrix4x4 ComputeNormalMatrix(Matrix4x4 pose)
    {
        if (Matrix4x4.Invert(pose, out var inv))
            return Matrix4x4.Transpose(inv);
        return Matrix4x4.Identity;
    }
}
