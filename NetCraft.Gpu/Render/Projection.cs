using System.Numerics;

namespace NetCraft.Gpu;

//Projection 3D 投影矩阵对标原版 net.minecraft.client.renderer.Projection
//GuiItemAtlas.drawToSlot 用 SetupOrtho(-1000,1000,texSize,texSize,true) 设正交投影
//invertY=true 让 Y 轴向下匹配 GUI 屏幕坐标方向
//PoC 阶段深度范围按 OpenGL -1..1 后续 Vulkan 后端需转 0..1
public sealed class Projection
{
    private Matrix4x4 _matrix = Matrix4x4.Identity;
    private bool _dirty = true;
    private float _zNear, _zFar, _width, _height, _fov;
    private bool _perspective, _invertY;

    //SetupOrtho 正交投影 GuiItemAtlas 用 invertY=true 翻转 Y 轴匹配 GUI 坐标
    public void SetupOrtho(float zNear, float zFar, float width, float height, bool invertY)
    {
        _zNear = zNear; _zFar = zFar;
        _width = width; _height = height;
        _invertY = invertY;
        _perspective = false;
        _dirty = true;
    }

    //SetupPerspective 透视投影世界渲染用 PoC 暂未接入
    public void SetupPerspective(float zNear, float zFar, float fov, float width, float height)
    {
        _zNear = zNear; _zFar = zFar;
        _fov = fov; _width = width; _height = height;
        _perspective = true;
        _dirty = true;
    }

    public void SetSize(float width, float height)
    {
        _width = width; _height = height;
        _dirty = true;
    }

    //GetMatrix 返回投影矩阵 dirty 时重算
    public Matrix4x4 GetMatrix()
    {
        if (!_dirty) return _matrix;
        if (_perspective)
        {
            var aspect = _width / _height;
            _matrix = Matrix4x4.CreatePerspectiveFieldOfView(_fov, aspect, _zNear, _zFar);
        }
        else
        {
            //原版 setOrtho(0,width, invertY?height:0, invertY?0:height, near, far)
            //invertY=true 时 bottom=height top=0 Y 轴向下
            var bottom = _invertY ? _height : 0f;
            var top = _invertY ? 0f : _height;
            _matrix = Matrix4x4.CreateOrthographicOffCenter(0f, _width, bottom, top, _zNear, _zFar);
        }
        _dirty = false;
        return _matrix;
    }
}
