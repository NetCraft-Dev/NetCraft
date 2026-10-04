using System.Numerics;

namespace NetCraft.Gpu;

//Lighting 方向光照对标原版 com.mojang.blaze3d.platform.Lighting
//预计算 5 个 Entry 的双光方向 vertex shader 用 dot(normal,lightDir) 算 diffuse
//DIFFUSE_LIGHT_0/1 是主光方向 flatPose/item3DPose 是各 Entry 的变换矩阵
//PoC 简化每 Entry 独立 UBO 不做原版切片对齐 GPU 部分需真 Vulkan 后端
public sealed class Lighting : IDisposable
{
    //DiffuseLight0 主光方向对标原版 DIFFUSE_LIGHT_0
    public static readonly Vector3 DiffuseLight0 = Vector3.Normalize(new Vector3(0.2f, 1.0f, -0.7f));
    //DiffuseLight1 副光方向对标原版 DIFFUSE_LIGHT_1
    public static readonly Vector3 DiffuseLight1 = Vector3.Normalize(new Vector3(-0.2f, 1.0f, 0.7f));

    //Entry 光照预设对标原版 Lighting.Entry
    public enum Entry { Level, ItemsFlat, Items3D, EntityInUi, PlayerSkin }

    //光方向数据两个 vec4 对标原版 UBO 内容 xyz 是光方向 w 未用 pad 到 32 bytes 匹配 std140
    public readonly record struct LightUniform(Vector4 Light0, Vector4 Light1);

    private readonly Dictionary<Entry, LightUniform> _lights = new();
    private readonly GpuDevice? _device;
    private Dictionary<Entry, GpuBuffer>? _ubos;
    private Dictionary<Entry, GpuDescriptorSet>? _sets;
    private GpuDescriptorLayout? _layout;
    private Entry _current = Entry.Items3D;

    public Lighting(GpuDevice? device = null)
    {
        _device = device;
        PrecomputeLights();
        //仅 Vulkan 等真后端创建 UBO Mock/Empty 后端无 CreateBuffer 跳过
        if (device?.SupportsGpuRendering == true)
            CreateUbos();
    }

    //PrecomputeLights 预计算各 Entry 光方向
    //对标原版构造时的 flatPose/item3DPose 矩阵变换 DIFFUSE_LIGHT_0/1
    private void PrecomputeLights()
    {
        //Level 用原始光方向
        _lights[Entry.Level] = new LightUniform(new Vector4(DiffuseLight0, 0f), new Vector4(DiffuseLight1, 0f));
        //ItemsFlat flatPose = rotationY(-0.3926991) * rotationX(2.3561945)
        var flatPose = Matrix4x4.CreateRotationY(-0.3926991f) * Matrix4x4.CreateRotationX(2.3561945f);
        _lights[Entry.ItemsFlat] = new LightUniform(
            new Vector4(Vector3.Normalize(Vector3.TransformNormal(DiffuseLight0, flatPose)), 0f),
            new Vector4(Vector3.Normalize(Vector3.TransformNormal(DiffuseLight1, flatPose)), 0f));
        //Items3D item3DPose = scale(1,-1,1) * rotateYXZ(1.0821041, 3.2375858, 0) * rotateYXZ(-0.3926991, 2.3561945, 0)
        var item3DPose = Matrix4x4.CreateScale(1, -1, 1)
            * Matrix4x4.CreateFromYawPitchRoll(1.0821041f, 3.2375858f, 0f)
            * Matrix4x4.CreateFromYawPitchRoll(-0.3926991f, 2.3561945f, 0f);
        _lights[Entry.Items3D] = new LightUniform(
            new Vector4(Vector3.Normalize(Vector3.TransformNormal(DiffuseLight0, item3DPose)), 0f),
            new Vector4(Vector3.Normalize(Vector3.TransformNormal(DiffuseLight1, item3DPose)), 0f));
        //EntityInUi 用 INVENTORY_DIFFUSE_LIGHT
        _lights[Entry.EntityInUi] = new LightUniform(
            new Vector4(Vector3.Normalize(new Vector3(0.2f, -1.0f, 1.0f)), 0f),
            new Vector4(Vector3.Normalize(new Vector3(-0.2f, -1.0f, 0.0f)), 0f));
        //PlayerSkin 用 playerSkinPose=identity 变换 INVENTORY_DIFFUSE_LIGHT
        _lights[Entry.PlayerSkin] = _lights[Entry.EntityInUi];
    }

    //CreateUbos 创建各 Entry 的 UBO + DescriptorSet
    //PoC 简化每 Entry 独立 UBO 不做原版切片对齐
    private void CreateUbos()
    {
        _ubos = new();
        _sets = new();
        var layoutDesc = new GpuDescriptorLayoutDescription();
        layoutDesc.Bindings.Add(new GpuDescriptorBinding
        {
            Binding = 0,
            DescriptorType = GpuDescriptorType.UniformBuffer,
            StageFlags = GpuShaderStageFlags.Vertex
        });
        _layout = _device!.CreateDescriptorLayout(layoutDesc);
        foreach (var (entry, light) in _lights)
        {
            var ubo = _device.CreateBuffer(32, GpuBufferUsage.UniformBuffer);
            ubo.Upload<LightUniform>(new[] { light });
            _ubos![entry] = ubo;
            var set = _device.AllocateDescriptorSet(_layout!);
            set.WriteBuffer(0, ubo, 0, -1);
            _sets![entry] = set;
        }
    }

    //GetLightDirections 返回指定 Entry 的光方向供 CPU 端光照计算/测试
    public LightUniform GetLightDirections(Entry entry) => _lights[entry];

    //SetupFor 设置当前光照 Entry 渲染时绑定对应 UBO
    public void SetupFor(Entry entry) => _current = entry;

    //CurrentDescriptorSet 当前 Entry 的 DescriptorSet 供 render pass 绑定
    //无 GPU 后端时返回 null
    public GpuDescriptorSet? CurrentDescriptorSet => _sets?.TryGetValue(_current, out var s) == true ? s : null;
    public GpuDescriptorLayout? Layout => _layout;
    public Entry Current => _current;

    public void Dispose()
    {
        if (_ubos is not null)
            foreach (var ubo in _ubos.Values) ubo.Dispose();
        if (_sets is not null)
            foreach (var set in _sets.Values) set.Dispose();
        _layout?.Dispose();
    }
}
