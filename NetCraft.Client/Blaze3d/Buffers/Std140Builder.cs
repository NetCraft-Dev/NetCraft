using System.Numerics;

namespace NetCraft.Client.Blaze3d.Buffers;

//Std140Builder writes std140-laid-out uniform data, aligns with vanilla Std140Builder
//Alignment follows the std140 rules: vec3 starts on a 16-byte boundary and occupies 16 bytes
public sealed class Std140Builder
{
    private readonly byte[] _buffer;
    private readonly int _start;
    private int _position;

    private Std140Builder(byte[] buffer)
    {
        _buffer = buffer;
        _start = 0;
        _position = 0;
    }

    //IntoBuffer writes into an existing buffer from its start
    public static Std140Builder IntoBuffer(byte[] buffer) => new Std140Builder(buffer);

    //OnStack allocates a writer over a size-byte scratch buffer
    public static Std140Builder OnStack(int size) => new Std140Builder(new byte[size]);

    //Get returns the bytes written so far
    public byte[] Get() => _buffer[.._position];

    public Std140Builder Align(int alignment)
    {
        _position = _start + RoundToward(_position - _start, alignment);
        return this;
    }

    public Std140Builder PutFloat(float value)
    {
        Align(4);
        Write(BitConverter.SingleToInt32Bits(value));
        return this;
    }

    public Std140Builder PutInt(int value)
    {
        Align(4);
        Write(value);
        return this;
    }

    public Std140Builder PutVec2(float x, float y)
    {
        Align(8);
        Write(BitConverter.SingleToInt32Bits(x));
        Write(BitConverter.SingleToInt32Bits(y));
        return this;
    }

    public Std140Builder PutVec2(Vector2 value) => PutVec2(value.X, value.Y);

    public Std140Builder PutIVec2(int x, int y)
    {
        Align(8);
        Write(x);
        Write(y);
        return this;
    }

    public Std140Builder PutVec3(float x, float y, float z)
    {
        Align(16);
        Write(BitConverter.SingleToInt32Bits(x));
        Write(BitConverter.SingleToInt32Bits(y));
        Write(BitConverter.SingleToInt32Bits(z));
        _position += 4;
        return this;
    }

    public Std140Builder PutVec3(Vector3 value) => PutVec3(value.X, value.Y, value.Z);

    public Std140Builder PutIVec3(int x, int y, int z)
    {
        Align(16);
        Write(x);
        Write(y);
        Write(z);
        _position += 4;
        return this;
    }

    public Std140Builder PutVec4(float x, float y, float z, float w)
    {
        Align(16);
        Write(BitConverter.SingleToInt32Bits(x));
        Write(BitConverter.SingleToInt32Bits(y));
        Write(BitConverter.SingleToInt32Bits(z));
        Write(BitConverter.SingleToInt32Bits(w));
        return this;
    }

    public Std140Builder PutVec4(Vector4 value) => PutVec4(value.X, value.Y, value.Z, value.W);

    public Std140Builder PutIVec4(int x, int y, int z, int w)
    {
        Align(16);
        Write(x);
        Write(y);
        Write(z);
        Write(w);
        return this;
    }

    public Std140Builder PutMat4f(Matrix4x4 value)
    {
        Align(16);
        //std140 mat4 is column-major; System.Numerics is row-major, so emit column by column
        Span<float> columns = stackalloc float[16]
        {
            value.M11, value.M21, value.M31, value.M41,
            value.M12, value.M22, value.M32, value.M42,
            value.M13, value.M23, value.M33, value.M43,
            value.M14, value.M24, value.M34, value.M44
        };
        foreach (var f in columns)
            Write(BitConverter.SingleToInt32Bits(f));
        return this;
    }

    private void Write(int value)
    {
        BitConverter.TryWriteBytes(_buffer.AsSpan(_position), value);
        _position += 4;
    }

    private static int RoundToward(int value, int divisor) => (value + divisor - 1) / divisor * divisor;
}
