using NetCraft.Util;

namespace NetCraft.Game.World.Entity;

//WalkAnimationState walk animation phase, maps to vanilla net.minecraft.world.entity.WalkAnimationState
//Holds the smoothed movement speed and the accumulated walk position that the limb swing animation reads
public sealed class WalkAnimationState
{
    private float _speedOld;
    private float _speed;
    private float _position;
    private float _positionScale = 1.0f;

    //SetSpeed sets the speed without advancing the position, maps to vanilla setSpeed
    public void SetSpeed(float speed) => _speed = speed;

    //Update advances the smoothed speed and the walk position, maps to vanilla update
    public void Update(float targetSpeed, float factor, float positionScale)
    {
        _speedOld = _speed;
        _speed += (targetSpeed - _speed) * factor;
        _position += _speed;
        _positionScale = positionScale;
    }

    //Stop resets the whole animation, maps to vanilla stop
    public void Stop()
    {
        _speedOld = 0f;
        _speed = 0f;
        _position = 0f;
    }

    //Speed the smoothed speed, maps to vanilla speed()
    public float Speed => _speed;

    //SpeedAt the smoothed speed interpolated for rendering, maps to vanilla speed(float)
    public float SpeedAt(float partialTicks) => Math.Min(Mth.Lerp(partialTicks, _speedOld, _speed), 1.0f);

    //Position the accumulated walk position, maps to vanilla position()
    public float Position => _position * _positionScale;

    //PositionAt the walk position interpolated for rendering, maps to vanilla position(float)
    public float PositionAt(float partialTicks) => (_position - _speed * (1.0f - partialTicks)) * _positionScale;

    //IsMoving whether the walk animation is still running, maps to vanilla isMoving
    public bool IsMoving => _speed > 1.0E-5f;
}
