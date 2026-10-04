using System.Numerics;

namespace NetCraft.Gpu;

//GpuLoadOp 附件加载策略 Clear 清屏 Load 保留之前内容
//ItemAtlas 多 slot 渲染用 Load 保留其他 slot 内容单 slot 渲染用 Clear
public enum GpuLoadOp
{
    Clear,
    Load
}

//GpuImageLayout 后端无关图像布局枚举供 TransitionImageLayout 用
//Vulkan 后端映射到 ImageLayout ColorAttachment 渲染附件 ShaderReadOnly 纹理采样
public enum GpuImageLayout
{
    ColorAttachment,
    ShaderReadOnly,
    TransferDst,
    TransferSrc
}

//ICommandEncoder 命令编码器对标原版 CommandEncoder
//录制 copy/clear/render pass 命令 Submit 后提交 GPU 队列
//替代旧 GpuCommandBuffer 的混合录制分离命令编码和渲染通道
public interface ICommandEncoder : IDisposable
{
    //CreateRenderPass 创建渲染通道 pipeline 提供 RenderPass 和 framebuffer
    //阶段 3 引入 dynamic rendering 后改为 RenderPass 和 pipeline 解耦恢复原版接口
    IRenderPass CreateRenderPass(CompiledRenderPipeline pipeline, GpuImage colorImage, Vector4 clearColor);
    IRenderPass CreateRenderPass(CompiledRenderPipeline pipeline, GpuImage colorImage, Vector4 clearColor, GpuImage depthImage, float clearDepth);
    //CreateRenderPass 带 colorLoadOp 重载 Load 保留之前 color 内容供多 slot 共享 atlas
    IRenderPass CreateRenderPass(CompiledRenderPipeline pipeline, GpuImage colorImage, Vector4 clearColor, GpuImage depthImage, float clearDepth, GpuLoadOp colorLoadOp);
    //CopyBuffer 拷贝源 buffer 到目标 buffer
    void CopyBuffer(GpuBuffer src, GpuBuffer dst, ulong srcOffset, ulong dstOffset, ulong size);
    //WriteToTexture 上传像素数据到图像走 staging 中转
    void WriteToTexture(GpuImage dst, ReadOnlySpan<byte> data, int dstX, int dstY, int width, int height);
    //TransitionImageLayout 录制图像布局转换到当前 command buffer 供 offscreen 渲染后转采样布局
    //VulkanImage.TransitionLayout 内部判断 currentLayout==newLayout 跳过重复转换安全
    void TransitionImageLayout(GpuImage image, GpuImageLayout newLayout);
    //Submit 提交所有录制命令到 GPU 队列并等待完成
    void Submit();
    //SubmitAsync 提交命令到 GPU 队列不等完成供 PIP 双缓冲异步渲染
    //调用方需在下次复用本 encoder 前 WaitForCompletion 保证 GPU 完成
    //staging buffer 延迟到 WaitForCompletion 释放
    void SubmitAsync() => throw new NotSupportedException("当前后端不支持异步 Submit");
    //WaitForCompletion 等 SubmitAsync 提交的 GPU 命令完成
    //未 SubmitAsync 过则 no-op 首次复用 encoder 安全跳过
    void WaitForCompletion() => throw new NotSupportedException("当前后端不支持异步 Submit");
    //BeginRecording 重新开始命令录制供 encoder 跨帧复用
    //首次调用幂等（构造已 Begin）后续调用 ResetCommandBuffer + BeginCommandBuffer
    //必须在 WaitForCompletion 后调用保证 GPU 不再使用 command buffer
    void BeginRecording() => throw new NotSupportedException("当前后端不支持 encoder 复用");
}
