using System.Numerics;
using NetCraft.Client.Blaze3d.Systems;
using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Textures;
using NetCraft.Client.Blaze3d.Shaders;
using NetCraft.Client.Blaze3d.Pipeline;
using NetCraft.Client.Blaze3d.Vertex;
using NetCraft.Client.Blaze3d.Vulkan;
using NetCraft.Client.Blaze3d.Platform;
using NetCraft.Client.Blaze3d.Font;
using NetCraft.Client.Blaze3d.Resource;
using NetCraft.Client.Blaze3d.Audio;
using NetCraft.Client.Blaze3d.Framegraph;
using NetCraft.Client.Blaze3d.Preprocessor;
using NetCraft.Client.Blaze3d.Util;
using NetCraft.Client.Render;
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

namespace NetCraft.Client.Blaze3d.Platform;

//Lighting directional lighting, maps to vanilla com.mojang.blaze3d.platform.Lighting
//Precomputes the dual light directions for 5 Entries; the vertex shader computes diffuse with dot(normal,lightDir)
//DIFFUSE_LIGHT_0/1 are the main light directions; flatPose/item3DPose are the per-Entry transform matrices
//The PoC simplifies to a separate UBO per Entry without the vanilla slice alignment; the GPU part needs a real Vulkan backend
public sealed class Lighting : IDisposable
{
    //DiffuseLight0 main light direction, maps to vanilla DIFFUSE_LIGHT_0
    public static readonly Vector3 DiffuseLight0 = Vector3.Normalize(new Vector3(0.2f, 1.0f, -0.7f));
    //DiffuseLight1 secondary light direction, maps to vanilla DIFFUSE_LIGHT_1
    public static readonly Vector3 DiffuseLight1 = Vector3.Normalize(new Vector3(-0.2f, 1.0f, 0.7f));

    //Entry lighting preset, maps to vanilla Lighting.Entry
    public enum Entry { Level, ItemsFlat, Items3D, EntityInUi, PlayerSkin }

    //Light direction data as two vec4, maps to the vanilla UBO contents; xyz is the light direction and w is unused, padded to 32 bytes to match std140
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
        //Only real backends like Vulkan create the UBO; the Mock/Empty backends have no CreateBuffer and are skipped
        if (device?.SupportsGpuRendering == true)
            CreateUbos();
    }

    //PrecomputeLights precomputes the light directions for each Entry
    //maps to the vanilla constructor transforming DIFFUSE_LIGHT_0/1 with the flatPose/item3DPose matrices
    private void PrecomputeLights()
    {
        //Level uses the raw light direction
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
        //EntityInUi uses INVENTORY_DIFFUSE_LIGHT
        _lights[Entry.EntityInUi] = new LightUniform(
            new Vector4(Vector3.Normalize(new Vector3(0.2f, -1.0f, 1.0f)), 0f),
            new Vector4(Vector3.Normalize(new Vector3(-0.2f, -1.0f, 0.0f)), 0f));
        //PlayerSkin transforms INVENTORY_DIFFUSE_LIGHT with playerSkinPose=identity
        _lights[Entry.PlayerSkin] = _lights[Entry.EntityInUi];
    }

    //CreateUbos creates the UBO + DescriptorSet for each Entry
    //The PoC simplifies to a separate UBO per Entry without the vanilla slice alignment
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

    //GetLightDirections returns the light directions of an Entry for CPU-side lighting computation/tests
    public LightUniform GetLightDirections(Entry entry) => _lights[entry];

    //SetupFor sets the current lighting Entry, binding its UBO on render
    public void SetupFor(Entry entry) => _current = entry;

    //CurrentDescriptorSet the current Entry's DescriptorSet for the render pass to bind
    //Returns null without a GPU backend
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
