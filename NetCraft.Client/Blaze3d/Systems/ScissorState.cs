namespace NetCraft.Client.Blaze3d.Systems;

//ScissorState dynamic scissor rectangle, aligns with vanilla com.mojang.blaze3d.systems.ScissorState
public sealed class ScissorState
{
    private bool _enabled;
    private int _x;
    private int _y;
    private int _width;
    private int _height;

    public ScissorState() { }

    public ScissorState(ScissorState state) => SetFrom(state);

    public void Enable(int x, int y, int width, int height)
    {
        _enabled = true;
        _x = x;
        _y = y;
        _width = width;
        _height = height;
    }

    public void Disable() => _enabled = false;

    public bool Enabled => _enabled;
    public int X => _x;
    public int Y => _y;
    public int Width => _width;
    public int Height => _height;

    public void SetFrom(ScissorState state)
    {
        _enabled = state._enabled;
        _x = state._x;
        _y = state._y;
        _width = state._width;
        _height = state._height;
    }
}
