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
namespace NetCraft.Client.Blaze3d.Pipeline;

//BlendFactor blend factor, maps to vanilla BlendFactor
//Controls the weight source of the source/destination colors during pipeline color blending
public enum BlendFactor
{
    Zero,
    One,
    SrcColor,
    OneMinusSrcColor,
    DstColor,
    OneMinusDstColor,
    SrcAlpha,
    OneMinusSrcAlpha,
    DstAlpha,
    OneMinusDstAlpha,
    ConstantColor,
    OneMinusConstantColor,
    ConstantAlpha,
    OneMinusConstantAlpha,
    SrcAlphaSaturate,
    Src1Color,
    OneMinusSrc1Color,
    Src1Alpha,
    OneMinusSrc1Alpha
}

//BlendOp blend operation, maps to vanilla BlendOp
//Controls how the factor-weighted source/destination colors combine
public enum BlendOp
{
    Add,
    Subtract,
    ReverseSubtract,
    Min,
    Max
}

//CompareOp depth/stencil compare operation, maps to vanilla CompareOp
public enum CompareOp
{
    Never,
    Less,
    Equal,
    LessOrEqual,
    Greater,
    NotEqual,
    GreaterOrEqual,
    Always
}

//PolygonMode polygon draw mode, maps to vanilla PolygonMode
public enum PolygonMode
{
    Fill,
    Line,
    Point
}

//PrimitiveTopology primitive topology, maps to vanilla PrimitiveTopology
public enum PrimitiveTopology
{
    Points,
    Lines,
    LineStrip,
    TriangleList,
    TriangleStrip,
    TriangleFan,
    Quads
}

//GpuFormat GPU pixel format, maps to vanilla GpuFormat
//ColorTargetState uses it to declare the attachment format
public enum GpuFormat
{
    R8Unorm,
    R8G8Unorm,
    R8G8B8A8Unorm,
    B8G8R8A8Unorm,
    R8G8B8A8Srgb,
    B8G8R8A8Srgb,
    R16Float,
    R16G16Float,
    R16G16B16A16Float,
    R32Float,
    R32G32Float,
    R32G32B32A32Float,
    D32Sfloat,
    D24UnormS8Uint,
    D16Unorm
}

//UniformType shader uniform type, maps to vanilla UniformType
//Used by BindGroupLayout.UniformDescription to describe the type of a uniform buffer/texture binding
public enum UniformType
{
    Mat4,
    Vec4,
    Vec3,
    Vec2,
    Float,
    Int,
    TexelBuffer,
    Sampler
}
