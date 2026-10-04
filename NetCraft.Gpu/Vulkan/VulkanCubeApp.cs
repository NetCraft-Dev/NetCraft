using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using Veldrid.SPIRV;
using ShaderStages = Veldrid.ShaderStages;
using VkFormat = Silk.NET.Vulkan.Format;

namespace NetCraft.Gpu.Vulkan;

//VulkanCubeApp 3D 立方体渲染示例
//验证深度测试+MVP uniform buffer+索引绘制+描述符集绑定完整链路
//每帧更新 model 旋转矩阵演示 uniform buffer 动态更新
public sealed unsafe class VulkanCubeApp : VulkanAppBase
{
    //CubeVertex 立方体顶点 vec3 position + vec3 color 共 24 字节
    [StructLayout(LayoutKind.Sequential)]
    private struct CubeVertex
    {
        public Vector3 Position;
        public Vector3 Color;
        public CubeVertex(float x, float y, float z, float r, float g, float b)
        {
            Position = new Vector3(x, y, z);
            Color = new Vector3(r, g, b);
        }
    }

    //MvpUniform 模型视图投影矩阵 column-major 上传到 shader
    //System.Numerics.Matrix4x4 是 row-major 上传前需要 transpose
    [StructLayout(LayoutKind.Sequential)]
    private struct MvpUniform
    {
        public Matrix4x4 Model;
        public Matrix4x4 View;
        public Matrix4x4 Proj;
    }

    private VulkanRenderPipeline _pipeline = null!;
    private VulkanBuffer _vertexBuffer = null!;
    private VulkanBuffer _indexBuffer = null!;
    private VulkanBuffer _uniformBuffer = null!;
    private VulkanDescriptorLayout _descriptorLayout = null!;
    private VulkanDescriptorSet _descriptorSet = null!;
    private VulkanImage _depthImage = null!;
    private float _rotation;

    public VulkanCubeApp() : base(800, 600) { }

    protected override string WindowTitle => "NetCraft.Gpu Vulkan Cube PoC";

    //4.3 改造 dynamic rendering 不再需要 GetFramebufferRenderPass 和 CreateFramebuffers
    //深度附件由 OnRecordCommandBuffer 传 _depthImage 给 BeginRenderPass

    protected override void OnCreatePipelineResources()
    {
        CreateDepthImage();
        CreateVertexBuffer();
        CreateUniformBuffer();
        CreateDescriptorSet();
        CreatePipeline();
    }

    //OnSwapchainRecreated swapchain 重建后重建 depth image 和 pipeline 因依赖 extent
    //descriptor set 已分配只需重新绑定 uniform buffer
    protected override void OnSwapchainRecreated()
    {
        _depthImage.Dispose();
        _pipeline.Dispose();
        CreateDepthImage();
        CreatePipeline();
        _descriptorSet.WriteBuffer(0, _uniformBuffer, 0, -1);
    }

    private void CreateDepthImage()
    {
        var desc = new GpuImageDescription
        {
            Width = (int)_swapchainExtent.Width,
            Height = (int)_swapchainExtent.Height,
            Format = GpuImageFormat.D32Sfloat,
            Usage = GpuImageUsage.DepthAttachment
        };
        _depthImage = (VulkanImage)_device.CreateImage(desc);
        //Upload 空像素触发 Undefined->DepthStencilAttachmentOptimal 布局转换
        _depthImage.Upload(ReadOnlySpan<byte>.Empty);
    }

    private void CreateVertexBuffer()
    {
        CubeVertex[] vertices =
        {
            new(-0.5f, -0.5f, -0.5f, 1f, 0f, 0f),
            new( 0.5f, -0.5f, -0.5f, 0f, 1f, 0f),
            new( 0.5f,  0.5f, -0.5f, 0f, 0f, 1f),
            new(-0.5f,  0.5f, -0.5f, 1f, 1f, 0f),
            new(-0.5f, -0.5f,  0.5f, 1f, 0f, 1f),
            new( 0.5f, -0.5f,  0.5f, 0f, 1f, 1f),
            new( 0.5f,  0.5f,  0.5f, 1f, 1f, 1f),
            new(-0.5f,  0.5f,  0.5f, 0f, 0f, 0f)
        };
        int size = vertices.Length * sizeof(CubeVertex);
        _vertexBuffer = (VulkanBuffer)_device.CreateBuffer(size, GpuBufferUsage.VertexBuffer);
        _vertexBuffer.Upload<CubeVertex>(vertices);
        ushort[] indices =
        {
            0, 1, 2,  0, 2, 3,
            4, 5, 6,  4, 6, 7,
            3, 2, 6,  3, 6, 7,
            0, 5, 1,  0, 4, 5,
            0, 3, 7,  0, 7, 4,
            1, 5, 6,  1, 6, 2
        };
        int indexSize = indices.Length * sizeof(ushort);
        _indexBuffer = (VulkanBuffer)_device.CreateBuffer(indexSize, GpuBufferUsage.IndexBuffer);
        _indexBuffer.Upload<ushort>(indices);
    }

    private void CreateUniformBuffer()
    {
        _uniformBuffer = (VulkanBuffer)_device.CreateBuffer(sizeof(MvpUniform), GpuBufferUsage.UniformBuffer);
    }

    private void CreateDescriptorSet()
    {
        var layoutDesc = new GpuDescriptorLayoutDescription();
        layoutDesc.Bindings.Add(new GpuDescriptorBinding
        {
            Binding = 0,
            DescriptorType = GpuDescriptorType.UniformBuffer,
            StageFlags = GpuShaderStageFlags.Vertex
        });
        _descriptorLayout = (VulkanDescriptorLayout)_device.CreateDescriptorLayout(layoutDesc);
        _descriptorSet = (VulkanDescriptorSet)_device.AllocateDescriptorSet(_descriptorLayout);
        _descriptorSet.WriteBuffer(0, _uniformBuffer, 0, -1);
    }

    private void CreatePipeline()
    {
        var vertSpv = CompileGlsl(VertexShaderSource, ShaderStages.Vertex);
        var fragSpv = CompileGlsl(FragmentShaderSource, ShaderStages.Fragment);
        var desc = new RenderPipelineDescription
        {
            VertexShaderSpirv = vertSpv,
            FragmentShaderSpirv = fragSpv,
            Topology = GpuPrimitiveTopology.TriangleList,
            BlendEnabled = false,
            DepthTestEnabled = true,
            TargetFormat = GpuImageFormat.B8G8R8A8Unorm,
            TargetWidth = (int)_swapchainExtent.Width,
            TargetHeight = (int)_swapchainExtent.Height
        };
        desc.VertexBindings.Add(new GpuVertexBinding
        {
            Binding = 0,
            Stride = sizeof(CubeVertex),
            Attributes =
            {
                new GpuVertexAttribute { Location = 0, Format = GpuVertexFormat.Vec3Float, Offset = 0 },
                new GpuVertexAttribute { Location = 1, Format = GpuVertexFormat.Vec3Float, Offset = 12 }
            }
        });
        desc.DescriptorLayouts.Add(_descriptorLayout);
        var fmt = VulkanRenderPipeline.ToVkFormat(desc.TargetFormat ?? GpuImageFormat.B8G8R8A8Unorm);
        _pipeline = new VulkanRenderPipeline(_device.Api, _device.Device, fmt, _swapchainExtent, desc);
    }

    //UpdateUniformBuffer 每帧更新 model 旋转矩阵 view/proj 固定
    //Matrix4x4 是 row-major 上传前 transpose 转 column-major 对齐 GLSL mat4
    private void UpdateUniformBuffer()
    {
        _rotation += 0.01f;
        var model = Matrix4x4.CreateRotationY(_rotation) * Matrix4x4.CreateRotationX(_rotation * 0.5f);
        var view = Matrix4x4.CreateLookAt(new Vector3(2, 2, 2), Vector3.Zero, Vector3.UnitY);
        var proj = Matrix4x4.CreatePerspectiveFieldOfView(60f * MathF.PI / 180f, (float)_swapchainExtent.Width / _swapchainExtent.Height, 0.1f, 10f);
        //Vulkan clip space Z 范围 [0,1] 不同于 OpenGL [-1,1] 需要修正 M33/M43
        proj.M33 = 0.1f / (0.1f - 10f);
        proj.M43 = (0.1f * 10f) / (0.1f - 10f);
        //Vulkan framebuffer Y 朝下 翻转 Y 使立方体正向显示
        proj.M22 *= -1;
        var mvp = new MvpUniform
        {
            Model = Matrix4x4.Transpose(model),
            View = Matrix4x4.Transpose(view),
            Proj = Matrix4x4.Transpose(proj)
        };
        _uniformBuffer.Upload<MvpUniform>(new[] { mvp });
    }

    //OnRecordCommandBuffer 4.3 改造传 colorImageView + _depthImage + clearDepth=1.0 走 dynamic rendering
    protected override void OnRecordCommandBuffer(VulkanCommandBuffer cmd, ImageView colorImageView)
    {
        UpdateUniformBuffer();
        cmd.BeginRecording();
        cmd.BeginRenderPass(_pipeline, colorImageView, _depthImage, 1.0f);
        cmd.BindVertexBuffer(_vertexBuffer, 0, 0);
        cmd.BindIndexBuffer(_indexBuffer, GpuIndexType.UInt16, 0);
        cmd.BindDescriptorSet(_descriptorSet, 0);
        cmd.DrawIndexed(36, 1, 0, 0, 0);
        cmd.EndRenderPass();
        cmd.EndRecording();
    }

    protected override void OnCleanupPipelineResources()
    {
        _descriptorSet.Dispose();
        _descriptorLayout.Dispose();
        _uniformBuffer.Dispose();
        _indexBuffer.Dispose();
        _vertexBuffer.Dispose();
        _depthImage.Dispose();
        _pipeline.Dispose();
    }

    private static byte[] CompileGlsl(string source, ShaderStages stage)
    {
        var fileName = stage == ShaderStages.Vertex ? "vert.glsl" : "frag.glsl";
        var result = SpirvCompilation.CompileGlslToSpirv(source, fileName, stage, new GlslCompileOptions());
        return result.SpirvBytes;
    }

    private const string VertexShaderSource = @"
#version 450
layout(location = 0) in vec3 inPosition;
layout(location = 1) in vec3 inColor;
layout(binding = 0) uniform UniformBufferObject {
    mat4 model;
    mat4 view;
    mat4 proj;
} ubo;
layout(location = 0) out vec3 fragColor;
void main() {
    gl_Position = ubo.proj * ubo.view * ubo.model * vec4(inPosition, 1.0);
    fragColor = inColor;
}
";

    private const string FragmentShaderSource = @"
#version 450
layout(location = 0) in vec3 fragColor;
layout(location = 0) out vec4 outColor;
void main() {
    outColor = vec4(fragColor, 1.0);
}
";
}
