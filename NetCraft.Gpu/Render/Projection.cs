using System.Numerics;

namespace NetCraft.Gpu;

//Projection 3D projection matrix, maps to vanilla net.minecraft.client.renderer.Projection
//GuiItemAtlas.drawToSlot sets the orthographic projection with SetupOrtho(-1000,1000,texSize,texSize,true)
//invertY=true makes Y point down to match the GUI screen coordinate direction
//The PoC stage uses the OpenGL depth range -1..1; the later Vulkan backend must convert to 0..1
public sealed class Projection
{
    private Matrix4x4 _matrix = Matrix4x4.Identity;
    private bool _dirty = true;
    private float _zNear, _zFar, _width, _height, _fov;
    private bool _perspective, _invertY;

    //SetupOrtho orthographic projection; GuiItemAtlas uses invertY=true to flip the Y axis to match GUI coordinates
    public void SetupOrtho(float zNear, float zFar, float width, float height, bool invertY)
    {
        _zNear = zNear; _zFar = zFar;
        _width = width; _height = height;
        _invertY = invertY;
        _perspective = false;
        _dirty = true;
    }

    //SetupPerspective perspective projection for world rendering; not wired in the PoC yet
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

    //GetMatrix returns the projection matrix, recomputed when dirty
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
            //Vanilla setOrtho(0,width, invertY?height:0, invertY?0:height, near, far)
            //With invertY=true bottom=height top=0 and Y points down
            var bottom = _invertY ? _height : 0f;
            var top = _invertY ? 0f : _height;
            _matrix = Matrix4x4.CreateOrthographicOffCenter(0f, _width, bottom, top, _zNear, _zFar);
        }
        _dirty = false;
        return _matrix;
    }
}
