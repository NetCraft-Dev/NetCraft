using System.Numerics;

namespace NetCraft.Game.Client.Render;

//Camera 世界渲染相机对标原版 net.minecraft.client.Camera
//持 Position/XRot/YRot 产 view 矩阵 + 投影矩阵（含 Vulkan Y/Z 矫正）
//rotation 用 Quaternion.CreateFromYawPitchRoll(-yRot, -xRot, 0) 对齐原版角度惯例
//Forward 默认 -Z（OpenGL 惯例）Up 默认 +Y Left 默认 -X
//第三人称 detached 留 TODO（alignWithEntity 被 decompiler 跳过 规则 28 不猜）
//xRot/yRot 参数为度数内部转弧度匹配原版 Entity.getXRot/getYRot 语义
public sealed class Camera
{
    private Vector3 _position = Vector3.Zero;
    private float _xRot, _yRot;
    private Quaternion _rotation = Quaternion.Identity;
    private Matrix4x4 _proj = Matrix4x4.Identity;
    private bool _projDirty = true;
    private float _fov = MathF.PI / 4f, _width = 800, _height = 600, _zNear = 0.05f, _zFar = 1000f;

    public Vector3 Position => _position;
    public float XRot => _xRot;
    public float YRot => _yRot;
    public Quaternion Rotation => _rotation;

    //Forward 相机前方向 yRot=0 xRot=0 时为 -Z
    public Vector3 Forward => Vector3.Transform(-Vector3.UnitZ, _rotation);
    //Up 相机上方向 默认 +Y 经 rotation 变换
    public Vector3 Up => Vector3.Transform(Vector3.UnitY, _rotation);
    //Left 相机左方向 默认 -X 经 rotation 变换
    public Vector3 Left => Vector3.Transform(-Vector3.UnitX, _rotation);

    //SetPosition 设置相机世界坐标
    public void SetPosition(Vector3 position) => _position = position;

    //SetRotation 设置相机旋转 yRot 偏航 xRot 俯仰 单位度
    //负号对齐原版 yaw/pitch 旋转方向（向左转 yRot 增大 Forward 向 +X 偏转）
    public void SetRotation(float yRot, float xRot)
    {
        _yRot = yRot;
        _xRot = xRot;
        var yRotRad = yRot * MathF.PI / 180f;
        var xRotRad = xRot * MathF.PI / 180f;
        _rotation = Quaternion.CreateFromYawPitchRoll(-yRotRad, -xRotRad, 0);
    }

    //UpdatePerspective 设置透视投影参数 dirty 时重算
    public void UpdatePerspective(float fov, float width, float height, float zNear, float zFar)
    {
        _fov = fov; _width = width; _height = height;
        _zNear = zNear; _zFar = zFar;
        _projDirty = true;
    }

    //GetProjectionMatrix 返回投影矩阵含 Vulkan Y 矫正
    //CreatePerspectiveFieldOfView 的 M33=far/(near-far) M43=near*far/(near-far) 已是 [0,1] z 范围
    //直接匹配 Vulkan NDC z 无需 Z 矫正（OpenGL 后端才需 M33*0.5+M43*0.5+0.5 转 [-1,1]→[0,1]）
    //Vulkan NDC Y 朝下与 OpenGL 相反需 M22/M42 翻转
    public Matrix4x4 GetProjectionMatrix()
    {
        if (_projDirty)
        {
            var aspect = _width / _height;
            _proj = Matrix4x4.CreatePerspectiveFieldOfView(_fov, aspect, _zNear, _zFar);
            //Vulkan Y 翻转 NDC Y 朝下
            _proj.M22 *= -1;
            _proj.M42 *= -1;
            _projDirty = false;
        }
        return _proj;
    }

    //GetViewMatrix 返回 view 矩阵 lookAt(position, position+forward, up)
    public Matrix4x4 GetViewMatrix()
        => Matrix4x4.CreateLookAt(_position, _position + Forward, Up);

    //GetViewProjMatrix 返回 view*proj（v*M 语义 先 view 后 proj）
    //上传 GLSL 后 shader 用 M*v 由 row-major/column-major 内存兼容等价 CPU v*M
    public Matrix4x4 GetViewProjMatrix()
        => GetViewMatrix() * GetProjectionMatrix();
}
