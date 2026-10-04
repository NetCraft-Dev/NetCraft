using System.Numerics;
using NetCraft.Gpu.Pipeline;

namespace NetCraft.Gpu;

//Draw 一次绘制调用的顶点索引元数据对标原版 Draw
//仅记录顶点布局与位置不持有 pipeline/texture/scissor 由上层 GuiRenderer 合批分组
public sealed class Draw
{
    public required VertexFormat VertexFormat { get; init; }
    public PrimitiveTopology Topology { get; init; }
    //BaseVertex 在 vertex buffer 中的起始顶点偏移 AppendDraw 时设为累计顶点数
    public int BaseVertex { get; internal set; }
    //VertexCount 顶点数量 VertexBuilder 写入时递增 EndDraw 后锁定
    public int VertexCount { get; internal set; }
    //VertexStartByte 该 Draw 顶点在 vertices 列表中的起始字节偏移供 GetVertexBytes 切片
    internal int VertexStartByte { get; set; }
    //FirstIndex 在 index buffer 中的起始索引偏移 QUADS 自动生成
    public int FirstIndex { get; internal set; }
    //IndexCount 索引数量 QUADS 为 vertexCount/4*6 其他为 0
    public int IndexCount { get; internal set; }
    //Ended 是否已 EndDraw 防止重复锁定
    internal bool Ended { get; set; }
}

//ExecuteInfo 绘制执行信息提交 GPU 阶段传给 IRenderPass
//VertexBuffer/IndexBuffer 为 null 表示该帧未上传或无索引
public sealed record ExecuteInfo(GpuBuffer VertexBuffer, GpuBuffer? IndexBuffer, int BaseVertex, int FirstIndex, int IndexCount);

//StagedVertexBuffer 分阶段顶点缓冲对标原版 StagedVertexBuffer
//submission phase 通过 AppendDraw+GetVertexBuilder 暂存顶点 EndDraw 自动 QUADS 索引
//Upload 阶段拼接所有 Draw 顶点到 vertex buffer+索引到 index buffer 上传 GPU
//CPU 暂存逻辑独立可单测 Upload 依赖 GpuDevice 集成测试才用真实 Vulkan
public sealed class StagedVertexBuffer : IDisposable
{
    private readonly List<Draw> _draws = new();
    private readonly List<byte> _vertices = new();
    private readonly AutoStorageIndexBuffer _indexBuffer = new();
    private GpuBuffer? _vertexBuffer;
    private int _totalVertexCount;
    private bool _disposed;

    //Draws 已添加的 Draw 列表供上层遍历提交
    public IReadOnlyList<Draw> Draws => _draws;

    //TotalVertexCount 当前帧累计顶点数供容量预估
    public int TotalVertexCount => _totalVertexCount;

    //AppendDraw 开始一次新 Draw 记录 baseVertex 为当前累计顶点数
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

    //GetVertexBuilder 返回 Draw 对应的 IVertexConsumer 按 VertexFormat 元素顺序写入
    //同 Draw 内多元素可不同 pose 不影响合批 pose 在此处 bake 进顶点位置
    public IVertexConsumer GetVertexBuilder(Draw draw)
    {
        var index = _draws.IndexOf(draw);
        if (index < 0) throw new ArgumentException("Draw 不属于此 StagedVertexBuffer", nameof(draw));
        return new VertexBuilder(this, draw);
    }

    //EndDraw 锁定 Draw 的 VertexCount 并对 QUADS 自动生成索引到 AutoStorageIndexBuffer
    //VertexBuilder 写入时已递增 draw.VertexCount 此处仅累加到全局并生成索引
    //重复调用安全 Ended 标记防止重复锁定
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

    //Upload 拼接所有 Draw 顶点到 vertex buffer 索引到 index buffer 上传 GPU
    //buffer 跨帧复用 size 不够才重建 HostVisible 每帧 map+memcpy 重写内容
    //修复旧实现 _vertexBuffer!=null 跳过 Upload 导致第二帧顶点数据没更新的 bug
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

    //GetExecuteInfo 返回 Draw 的执行信息提交阶段传给 IRenderPass
    public ExecuteInfo GetExecuteInfo(Draw draw)
    {
        return new ExecuteInfo(_vertexBuffer!, _indexBuffer.IndexBuffer, draw.BaseVertex, draw.FirstIndex, draw.IndexCount);
    }

    //EndFrame 重置暂存区保留 GPU buffer 跨帧复用
    public void EndFrame()
    {
        _draws.Clear();
        _vertices.Clear();
        _indexBuffer.EndFrame();
        _totalVertexCount = 0;
    }

    //GetVertexBytes 返回 Draw 的顶点字节快照供单测验证布局
    public byte[] GetVertexBytes(Draw draw)
    {
        var len = draw.VertexCount * draw.VertexFormat.Stride;
        return _vertices.GetRange(draw.VertexStartByte, len).ToArray();
    }

    //IndexBuffer 暴露索引缓冲供单测验证 QUADS 自动索引
    internal AutoStorageIndexBuffer IndexBuffer => _indexBuffer;

    public void Dispose()
    {
        if (_disposed) return;
        _vertexBuffer?.Dispose();
        _vertexBuffer = null;
        _indexBuffer.Dispose();
        _disposed = true;
    }

    //VertexBuilder StagedVertexBuffer 的 IVertexConsumer 实现
    //按 Draw.VertexFormat 的 elements 顺序写入顶点数据
    //支持 Position(Vec3)/UV0(Vec2)/Color(UByte4Norm) 三种元素名称匹配
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
                        //2D 格式 Color 是 UByte4Norm ARGB int 拆 RGBA 字节顺序
                        _buffer._vertices.Add((byte)((color >> 16) & 0xFF));
                        _buffer._vertices.Add((byte)((color >> 8) & 0xFF));
                        _buffer._vertices.Add((byte)(color & 0xFF));
                        _buffer._vertices.Add((byte)((color >> 24) & 0xFF));
                        break;
                    default:
                        //未知元素填零保持 stride 对齐
                        for (int i = 0; i < SizeOf(elem.Format); i++)
                            _buffer._vertices.Add((byte)0);
                        break;
                }
            }
            _draw.VertexCount++;
        }

        //AddVertex3D 写 3D 顶点 position+color+uv+light+normal 对应 POSITION_COLOR_UV_LIGHT_NORMAL
        //position 和 normal 由调用方预先变换好 color/light 是 int 当 float 位模式写入 shader 用 int() 解包
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
                        //3D 格式 Color 是 Float ARGB int 数值转换写入 shader int() 还原
                        //不能用位模式转换 0xFFFFFFFF 位模式是 NaN shader int(NaN) 未定义
                        WriteFloat(_buffer._vertices, (float)color);
                        break;
                    case "UV0":
                        WriteFloat(_buffer._vertices, u);
                        WriteFloat(_buffer._vertices, v);
                        break;
                    case "Light":
                        //Light 是 Float (block<<4)|(sky<<20) packed int 数值转换写入
                        //light 值在 2^24 内 float 精确表示无精度损失
                        WriteFloat(_buffer._vertices, (float)light);
                        break;
                    case "Normal":
                        WriteFloat(_buffer._vertices, nx);
                        WriteFloat(_buffer._vertices, ny);
                        WriteFloat(_buffer._vertices, nz);
                        break;
                    default:
                        //未知元素填零保持 stride 对齐
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
