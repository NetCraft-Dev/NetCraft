using System.Numerics;
using NetCraft.Client.Blaze3d.Pipeline;
using NetCraft.Client.Render;
using NetCraft.Client.Blaze3d.Systems;
using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Textures;
using NetCraft.Client.Blaze3d.Shaders;
using NetCraft.Client.Blaze3d.Vertex;
using NetCraft.Client.Blaze3d.Vulkan;
using NetCraft.Client.Blaze3d.Platform;
using NetCraft.Client.Blaze3d.Font;
using NetCraft.Client.Blaze3d.Resource;
using NetCraft.Client.Blaze3d.Audio;
using NetCraft.Client.Blaze3d.Framegraph;
using NetCraft.Client.Blaze3d.Preprocessor;
using NetCraft.Client.Blaze3d.Util;
using NetCraft.Client.Render.Model;
using NetCraft.Client.Render.Texture;
using NetCraft.Client.Render.Texture.Atlas;
using NetCraft.Client.Render.Item;
using NetCraft.Client.Render.Entity;
using NetCraft.Client.Render.Entity.State;
using NetCraft.Client.Render.State.Gui;
using NetCraft.Client.Gui;
using NetCraft.Client.Gui.Render;
using NetCraft.Client.Gui.Render.State;
using NetCraft.Client.Gui.Render.Pip;
using NetCraft.Client.Gui.Navigation;
using NetCraft.Client.Gui.Layouts;
using NetCraft.Client.Gui.Font;
using NetCraft.Client.Gui.Font.Providers;
using NetCraft.Client.Gui.Font.Glyphs;
using NetCraft.Client.Model;
using NetCraft.Client.Model.Geom;
using NetCraft.Client.Resources.Metadata.Gui;

namespace NetCraft.Client.Render;

//Draw vertex/index metadata for one draw call, maps to vanilla Draw
//Only records vertex layout and position without holding pipeline/texture/scissor; the upper GuiRenderer batches and groups
public sealed class Draw
{
    public required VertexFormat VertexFormat { get; init; }
    public PrimitiveTopology Topology { get; init; }
    //BaseVertex starting vertex offset in the vertex buffer, set to the accumulated vertex count in AppendDraw
    public int BaseVertex { get; internal set; }
    //VertexCount vertex count incremented by VertexBuilder writes and locked after EndDraw
    public int VertexCount { get; internal set; }
    //VertexStartByte the Draw's vertex start byte offset in the vertices list, for GetVertexBytes to slice
    internal int VertexStartByte { get; set; }
    //FirstIndex starting index offset in the index buffer, auto-generated for QUADS
    public int FirstIndex { get; internal set; }
    //IndexCount index count, vertexCount/4*6 for QUADS and 0 otherwise
    public int IndexCount { get; internal set; }
    //Ended whether EndDraw was called, preventing a repeated lock
    internal bool Ended { get; set; }
}

//ExecuteInfo draw execution info passed to IRenderPass in the GPU submission phase
//VertexBuffer/IndexBuffer null means not uploaded this frame or no indices
public sealed record ExecuteInfo(GpuBuffer VertexBuffer, GpuBuffer? IndexBuffer, int BaseVertex, int FirstIndex, int IndexCount);

//StagedVertexBuffer staged vertex buffer, maps to vanilla StagedVertexBuffer
//The submission phase stages vertices via AppendDraw+GetVertexBuilder; EndDraw auto-generates QUADS indices
//The Upload phase stitches all Draw vertices into the vertex buffer + indices into the index buffer and uploads to the GPU
//The CPU staging logic is standalone and unit-testable; Upload depends on GpuDevice and only integration tests use real Vulkan
public sealed class StagedVertexBuffer : IDisposable
{
    private readonly List<Draw> _draws = new();
    private readonly List<byte> _vertices = new();
    private readonly AutoStorageIndexBuffer _indexBuffer = new();
    private GpuBuffer? _vertexBuffer;
    private int _totalVertexCount;
    private bool _disposed;

    //Draws the added Draw list for the upper layer to iterate and submit
    public IReadOnlyList<Draw> Draws => _draws;

    //TotalVertexCount the frame's accumulated vertex count for capacity estimation
    public int TotalVertexCount => _totalVertexCount;

    //AppendDraw starts a new Draw, recording baseVertex as the current accumulated vertex count
    public Draw AppendDraw(VertexFormat format, PrimitiveTopology topology)
    {
        var draw = new Draw
        {
            VertexFormat = format,
            Topology = topology,
            BaseVertex = _totalVertexCount,
            VertexStartByte = _vertices.Count
        };
        _draws.Add(draw);
        return draw;
    }

    //GetVertexBuilder returns the Draw's IVertexConsumer, writing in VertexFormat element order
    //Multiple elements in one Draw may use different poses without affecting batching; the pose is baked into the vertex position here
    public IVertexConsumer GetVertexBuilder(Draw draw)
    {
        var index = _draws.IndexOf(draw);
        if (index < 0) throw new ArgumentException("Draw does not belong to this StagedVertexBuffer", nameof(draw));
        return new VertexBuilder(this, draw);
    }

    //EndDraw locks the Draw's VertexCount and auto-generates QUADS indices into AutoStorageIndexBuffer
    //VertexBuilder writes already incremented draw.VertexCount; here it only accumulates globally and generates indices
    //Repeated calls are safe; the Ended flag prevents a repeated lock
    public void EndDraw(Draw draw)
    {
        if (draw.Ended) return;
        _totalVertexCount += draw.VertexCount;
        if (draw.Topology == PrimitiveTopology.Quads)
        {
            var (firstIndex, indexCount) = _indexBuffer.Append(draw.BaseVertex, draw.VertexCount, draw.Topology);
            draw.FirstIndex = firstIndex;
            draw.IndexCount = indexCount;
        }
        draw.Ended = true;
    }

    //Upload stitches all Draw vertices into the vertex buffer and indices into the index buffer, then uploads to the GPU
    //The buffer is reused across frames and rebuilt only when too small; host-visible, map+memcpy rewrites it each frame
    //Fixed the old bug where _vertexBuffer!=null skipped Upload, leaving the second frame's vertex data unupdated
    public void Upload(GpuDevice device)
    {
        if (_vertices.Count == 0)
        {
            _indexBuffer.Upload(device);
            return;
        }
        if (_vertexBuffer == null || _vertexBuffer.Size < _vertices.Count)
        {
            _vertexBuffer?.Dispose();
            _vertexBuffer = device.CreateHostVisibleBuffer(_vertices.Count, GpuBufferUsage.VertexBuffer);
        }
        _vertexBuffer.Upload<byte>(_vertices.ToArray());
        _indexBuffer.Upload(device);
    }

    //GetExecuteInfo returns the Draw's execution info, passed to IRenderPass in the submission phase
    public ExecuteInfo GetExecuteInfo(Draw draw)
    {
        return new ExecuteInfo(_vertexBuffer!, _indexBuffer.IndexBuffer, draw.BaseVertex, draw.FirstIndex, draw.IndexCount);
    }

    //EndFrame resets the staging area while keeping GPU buffers for cross-frame reuse
    public void EndFrame()
    {
        _draws.Clear();
        _vertices.Clear();
        _indexBuffer.EndFrame();
        _totalVertexCount = 0;
    }

    //GetVertexBytes returns the Draw's vertex byte snapshot for unit tests to verify the layout
    public byte[] GetVertexBytes(Draw draw)
    {
        var len = draw.VertexCount * draw.VertexFormat.Stride;
        return _vertices.GetRange(draw.VertexStartByte, len).ToArray();
    }

    //IndexBuffer exposes the index buffer for unit tests to verify QUADS auto-indexing
    internal AutoStorageIndexBuffer IndexBuffer => _indexBuffer;

    public void Dispose()
    {
        if (_disposed) return;
        _vertexBuffer?.Dispose();
        _vertexBuffer = null;
        _indexBuffer.Dispose();
        _disposed = true;
    }

    //VertexBuilder the IVertexConsumer implementation of StagedVertexBuffer
    //Writes vertex data in the order of Draw.VertexFormat's elements
    //Matches three element names Position(Vec3)/UV0(Vec2)/Color(UByte4Norm)
    private sealed class VertexBuilder : IVertexConsumer
    {
        private readonly StagedVertexBuffer _buffer;
        private readonly Draw _draw;

        public VertexBuilder(StagedVertexBuffer buffer, Draw draw)
        {
            _buffer = buffer;
            _draw = draw;
        }

        public void AddVertexWith2DPose(Matrix3x2 pose, float x, float y, float u, float v, int color)
        {
            var pos = Vector2.Transform(new Vector2(x, y), pose);
            foreach (var elem in _draw.VertexFormat.Elements)
            {
                switch (elem.Name)
                {
                    case "Position":
                        WriteFloat(_buffer._vertices, pos.X);
                        WriteFloat(_buffer._vertices, pos.Y);
                        WriteFloat(_buffer._vertices, 0f);
                        break;
                    case "UV0":
                        WriteFloat(_buffer._vertices, u);
                        WriteFloat(_buffer._vertices, v);
                        break;
                    case "Color":
                        //2D format Color is UByte4Norm ARGB int split into RGBA byte order
                        _buffer._vertices.Add((byte)((color >> 16) & 0xFF));
                        _buffer._vertices.Add((byte)((color >> 8) & 0xFF));
                        _buffer._vertices.Add((byte)(color & 0xFF));
                        _buffer._vertices.Add((byte)((color >> 24) & 0xFF));
                        break;
                    default:
                        //Unknown elements are zero-filled to keep the stride aligned
                        for (int i = 0; i < SizeOf(elem.Format); i++)
                            _buffer._vertices.Add((byte)0);
                        break;
                }
            }
            _draw.VertexCount++;
        }

        //AddVertex3D writes 3D vertices position+color+uv+light+normal, corresponding to POSITION_COLOR_UV_LIGHT_NORMAL
        //position and normal are pre-transformed by the caller; color/light are ints written as float bit patterns and unpacked with int() in the shader
        public void AddVertex3D(float x, float y, float z, int color,
            float u, float v, int light, float nx, float ny, float nz)
        {
            foreach (var elem in _draw.VertexFormat.Elements)
            {
                switch (elem.Name)
                {
                    case "Position":
                        WriteFloat(_buffer._vertices, x);
                        WriteFloat(_buffer._vertices, y);
                        WriteFloat(_buffer._vertices, z);
                        break;
                    case "Color":
                        //3D format Color is a Float; the ARGB int is written as a converted value and restored with int() in the shader
                        //A bit-pattern conversion cannot be used; 0xFFFFFFFF's bit pattern is NaN and shader int(NaN) is undefined
                        WriteFloat(_buffer._vertices, (float)color);
                        break;
                    case "UV0":
                        WriteFloat(_buffer._vertices, u);
                        WriteFloat(_buffer._vertices, v);
                        break;
                    case "Light":
                        //Light is a Float; the (block<<4)|(sky<<20) packed int is written as a converted value
                        //Light values within 2^24 are exactly representable as float with no precision loss
                        WriteFloat(_buffer._vertices, (float)light);
                        break;
                    case "Normal":
                        WriteFloat(_buffer._vertices, nx);
                        WriteFloat(_buffer._vertices, ny);
                        WriteFloat(_buffer._vertices, nz);
                        break;
                    default:
                        //Unknown elements are zero-filled to keep the stride aligned
                        for (int i = 0; i < SizeOf(elem.Format); i++)
                            _buffer._vertices.Add((byte)0);
                        break;
                }
            }
            _draw.VertexCount++;
        }

        private static void WriteFloat(List<byte> list, float value)
        {
            var bytes = BitConverter.GetBytes(value);
            list.AddRange(bytes);
        }

        private static int SizeOf(VertexElementFormat f) => f switch
        {
            VertexElementFormat.Float => 4,
            VertexElementFormat.Vec2 => 8,
            VertexElementFormat.Vec3 => 12,
            VertexElementFormat.Vec4 => 16,
            VertexElementFormat.UByte4Norm => 4,
            VertexElementFormat.UShort2Norm => 4,
            VertexElementFormat.Int => 4,
            VertexElementFormat.IVec2 => 8,
            _ => 4
        };
    }
}
