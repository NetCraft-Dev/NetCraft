using System.Numerics;

namespace NetCraft.Game.Client.Render;

//Camera world render camera, maps to vanilla net.minecraft.client.Camera
//Holds Position/XRot/YRot and produces the view matrix + projection matrix (with Vulkan Y/Z correction)
//rotation uses Quaternion.CreateFromYawPitchRoll(-yRot, -xRot, 0), aligning with vanilla angle conventions
//Forward default -Z (OpenGL convention), Up default +Y, Left default -X
//Third-person detached left as TODO (alignWithEntity was skipped by the decompiler; rule 28: do not guess)
//xRot/yRot parameters are in degrees and converted to radians internally, matching vanilla Entity.getXRot/getYRot semantics
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

    //Forward camera forward direction, -Z when yRot=0 xRot=0
    public Vector3 Forward => Vector3.Transform(-Vector3.UnitZ, _rotation);
    //Up camera up direction, default +Y transformed by rotation
    public Vector3 Up => Vector3.Transform(Vector3.UnitY, _rotation);
    //Left camera left direction, default -X transformed by rotation
    public Vector3 Left => Vector3.Transform(-Vector3.UnitX, _rotation);

    //SetPosition sets the camera world position
    public void SetPosition(Vector3 position) => _position = position;

    //SetRotation sets the camera rotation: yRot yaw, xRot pitch, in degrees
    //The negative signs align with vanilla yaw/pitch rotation direction (turning left increases yRot and Forward tilts toward +X)
    public void SetRotation(float yRot, float xRot)
    {
        _yRot = yRot;
        _xRot = xRot;
        var yRotRad = yRot * MathF.PI / 180f;
        var xRotRad = xRot * MathF.PI / 180f;
        _rotation = Quaternion.CreateFromYawPitchRoll(-yRotRad, -xRotRad, 0);
    }

    //UpdatePerspective sets the perspective projection parameters and recomputes when dirty
    public void UpdatePerspective(float fov, float width, float height, float zNear, float zFar)
    {
        _fov = fov; _width = width; _height = height;
        _zNear = zNear; _zFar = zFar;
        _projDirty = true;
    }

    //GetProjectionMatrix returns the projection matrix including Vulkan Y correction
    //CreatePerspectiveFieldOfView's M33=far/(near-far) M43=near*far/(near-far) is already the [0,1] z range
    //This directly matches Vulkan NDC z with no Z correction (only the OpenGL backend needs M33*0.5+M43*0.5+0.5 to convert [-1,1]→[0,1])
    //Vulkan NDC Y points down, opposite to OpenGL, so M22/M42 must be flipped
    public Matrix4x4 GetProjectionMatrix()
    {
        if (_projDirty)
        {
            var aspect = _width / _height;
            _proj = Matrix4x4.CreatePerspectiveFieldOfView(_fov, aspect, _zNear, _zFar);
            //Vulkan Y flip; NDC Y points down
            _proj.M22 *= -1;
            _proj.M42 *= -1;
            _projDirty = false;
        }
        return _proj;
    }

    //GetViewMatrix returns the view matrix lookAt(position, position+forward, up)
    public Matrix4x4 GetViewMatrix()
        => Matrix4x4.CreateLookAt(_position, _position + Forward, Up);

    //GetViewProjMatrix returns view*proj (v*M semantics: view then proj)
    //Uploaded to GLSL, the shader uses M*v, which is equivalent to CPU v*M thanks to row-major/column-major memory compatibility
    public Matrix4x4 GetViewProjMatrix()
        => GetViewMatrix() * GetProjectionMatrix();
}
