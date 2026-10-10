using NetCraft.Client.Render.Model;
using NetCraft.Client.Blaze3d.Systems;
using NetCraft.Client.Blaze3d.Buffers;
using NetCraft.Client.Blaze3d.Textures;
using NetCraft.Client.Blaze3d.Shaders;
using NetCraft.Client.Blaze3d.Pipeline;
using NetCraft.Client.Blaze3d.Vertex;
using NetCraft.Client.Blaze3d.Vulkan;
using NetCraft.Client.Blaze3d.Platform;
using NetCraft.Client.Blaze3d.Font;
using NetCraft.Registry.State;
using NetCraft.Storage.Chunk;
using NetCraft.Client.Blaze3d.Resource;
using NetCraft.Client.Blaze3d.Audio;
using NetCraft.Client.Blaze3d.Framegraph;
using NetCraft.Client.Blaze3d.Preprocessor;
using NetCraft.Client.Blaze3d.Util;
using NetCraft.Client.Render;
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

namespace NetCraft.Client.Render.World;

//ChunkMeshBuilder chunk mesh generator, maps to vanilla SectionCompiler
//Iterates the LevelChunkSection's 16³ blocks; for non-empty blocks looks up BakedModel and culls faces by cullface + neighbor BlockRenderShape
//cullface quads: if the neighbor is FullBlock, cull that direction's face; no-cull quads are always rendered
//Vertex positions come from BlockModelBaker in 0-16 range, normalized to 0-1 by PoseStack scale(1/16), then translated to the block position by translate(x,y,z)
//originX/Y/Z is the section's world base coordinate, used for light queries; the first version does no cross-section neighbor queries, so out-of-range 0-15 is treated as air and not culled
//Light goes through ChunkLightSampler to fetch the face's outer neighbor block/sky light; when null, FullBright is used for old tests
//face-based shading bakes a fixed shade factor per quad.Direction into Color RGB, aligning with vanilla face picking
//Fluid rendering is not included; FluidRenderer is a separate subsystem to be added later
public sealed class ChunkMeshBuilder
{
    private readonly Func<BlockState, BakedModel?> _modelMapper;
    private readonly ChunkLightSampler? _lightSampler;

    public ChunkMeshBuilder(BlockStateModelMapper modelMapper, ChunkLightSampler? lightSampler = null)
        : this(modelMapper.GetModel, lightSampler) { }

    //Func constructor for tests to inject a stub mapping, avoiding a ResourceManager dependency
    public ChunkMeshBuilder(Func<BlockState, BakedModel?> modelMapper, ChunkLightSampler? lightSampler = null)
    {
        _modelMapper = modelMapper;
        _lightSampler = lightSampler;
    }

    //Build iterates the section's 16³ blocks to generate mesh data; originX/Y/Z is the section's world base coordinate
    //regionCache=null uses the W7 out-of-bounds-as-air logic for compatibility with old unit tests
    public ChunkMeshData Build(LevelChunkSection section, int originX = 0, int originY = 0, int originZ = 0)
        => BuildCore(section, null, originX, originY, originZ);

    //Build overload taking a RenderRegionCache for cross-section neighbor queries, fixing boundary face culling
    //regionCache.Center must match the SectionPos of the section
    public ChunkMeshData Build(LevelChunkSection section, RenderRegionCache regionCache, int originX, int originY, int originZ)
        => BuildCore(section, regionCache, originX, originY, originZ);

    //BuildBlock bakes a single block's model into the mesh under the given transform, for moving blocks like pistons that re-render each frame
    //The chunk mesh is baked statically; a displacement that changes every frame cannot be baked in, so it goes through a separate dynamic pass
    //A moving block's neighbor relationships change at any time, so faces in all six directions are never culled; light is fetched from the block's own cell
    public void BuildBlock(BlockState state, NetCraft.Primitives.BlockPos pos, PoseStack pose,
        ChunkMeshData mesh)
    {
        var model = _modelMapper(state);
        if (model is null) return;
        var instance = new QuadInstance();
        foreach (var layer in model.Layers)
        {
            EmitMovingQuads(mesh, layer, model.GetNoCullQuads(layer), pos, pose, instance);
            foreach (var dir in AllDirections)
                EmitMovingQuads(mesh, layer, model.GetCullfaceQuads(layer, dir), pos, pose, instance);
        }
    }

    //EmitMovingQuads writes a batch of moving block quads; light is fetched from the block's own cell
    private void EmitMovingQuads(ChunkMeshData mesh, RenderLayer layer, IReadOnlyList<BakedQuad> quads,
        NetCraft.Primitives.BlockPos pos, PoseStack pose, QuadInstance instance)
    {
        if (quads.Count == 0) return;
        var consumer = mesh.GetOrBeginLayer(layer);
        for (var i = 0; i < quads.Count; i++)
        {
            var quad = quads[i];
            instance.Color = ApplyFaceShade(-1, quad.Direction);
            instance.LightCoords = _lightSampler is null
                ? LightTexture.FullBrightCoords
                : _lightSampler.GetLightCoords(pos.X, pos.Y, pos.Z, quad.LightEmission);
            VertexConsumer3D.PutBakedQuad(consumer, pose, quad, instance);
        }
    }

    //BuildCore shared compile logic; cross-section face culling when regionCache!=null, otherwise W7 out-of-bounds-as-air
    private ChunkMeshData BuildCore(LevelChunkSection section, RenderRegionCache? regionCache, int originX, int originY, int originZ)
    {
        var mesh = new ChunkMeshData();
        var pose = new PoseStack();
        for (var x = 0; x < 16; x++)
        for (var y = 0; y < 16; y++)
        for (var z = 0; z < 16; z++)
        {
            var state = section.GetBlockState(x, y, z);
            if (BlockRenderShapeProvider.GetShape(state) == BlockRenderShape.Empty)
                continue;
            var model = _modelMapper(state);
            if (model is null)
                continue;
            pose.PushPose();
            pose.Scale(1f / 16f, 1f / 16f, 1f / 16f);
            //translate adds sectionOrigin to bake vertices into world coordinates; shader-side Model=Identity
            pose.Translate(x + originX, y + originY, z + originZ);
            AddBlockQuads(pose, section, regionCache, x, y, z, originX, originY, originZ, model, mesh);
            pose.PopPose();
        }
        return mesh;
    }

    //AddBlockQuads writes the block's BakedModel quads into the mesh by layer + cullface
    private void AddBlockQuads(PoseStack pose, LevelChunkSection section, RenderRegionCache? regionCache,
        int x, int y, int z, int originX, int originY, int originZ, BakedModel model, ChunkMeshData mesh)
    {
        var instance = new QuadInstance();
        foreach (var layer in model.Layers)
        {
            //cullface quads: look up the neighbor by direction and cull if FullBlock
            AddCullfaceQuads(pose, section, regionCache, x, y, z, originX, originY, originZ, model, layer, mesh, instance);
            //no-cull quads: always rendered
            AddNoCullQuads(pose, x, y, z, originX, originY, originZ, model, layer, mesh, instance);
        }
    }

    //AddCullfaceQuads iterates the 6-direction cullface quads, skipping when the neighbor is FullBlock
    //regionCache!=null queries the neighbor across sections, otherwise W7 out-of-bounds-as-air
    private void AddCullfaceQuads(PoseStack pose, LevelChunkSection section, RenderRegionCache? regionCache,
        int x, int y, int z, int originX, int originY, int originZ,
        BakedModel model, RenderLayer layer, ChunkMeshData mesh, QuadInstance instance)
    {
        foreach (var dir in AllDirections)
        {
            var quads = model.GetCullfaceQuads(layer, dir);
            if (quads.Count == 0) continue;
            if (ShouldCullFace(section, regionCache, x, y, z, originX, originY, originZ, dir)) continue;
            var consumer = mesh.GetOrBeginLayer(layer);
            for (var i = 0; i < quads.Count; i++)
            {
                var quad = quads[i];
                instance.Color = ApplyFaceShade(-1, quad.Direction);
                instance.LightCoords = GetLightForFace(x, y, z, originX, originY, originZ, dir, quad.LightEmission);
                VertexConsumer3D.PutBakedQuad(consumer, pose, quad, instance);
            }
        }
    }

    //AddNoCullQuads writes quads without cullface; always rendered
    private void AddNoCullQuads(PoseStack pose, int x, int y, int z, int originX, int originY, int originZ,
        BakedModel model, RenderLayer layer, ChunkMeshData mesh, QuadInstance instance)
    {
        var quads = model.GetNoCullQuads(layer);
        if (quads.Count == 0) return;
        var consumer = mesh.GetOrBeginLayer(layer);
        for (var i = 0; i < quads.Count; i++)
        {
            var quad = quads[i];
            instance.Color = ApplyFaceShade(-1, quad.Direction);
            instance.LightCoords = GetLightForFace(x, y, z, originX, originY, originZ, quad.Direction, quad.LightEmission);
            VertexConsumer3D.PutBakedQuad(consumer, pose, quad, instance);
        }
    }

    //ShouldCullFace decides whether the current block's face in a direction is occluded by a neighbor and should be culled
    //regionCache!=null queries the neighbor's world coordinates across sections, otherwise W7 out-of-bounds-as-air (not culled)
    //Cull when the neighbor's BlockRenderShape==FullBlock; Custom/Empty are not culled
    private static bool ShouldCullFace(LevelChunkSection section, RenderRegionCache? regionCache,
        int x, int y, int z, int originX, int originY, int originZ, Direction dir)
    {
        if (regionCache is not null)
            return regionCache.ShouldCullFace(originX + x, originY + y, originZ + z, dir);
        var offset = dir.UnitVector();
        var nx = x + (int)offset.X;
        var ny = y + (int)offset.Y;
        var nz = z + (int)offset.Z;
        if ((uint)nx >= 16 || (uint)ny >= 16 || (uint)nz >= 16)
            return false;
        var neighbor = section.GetBlockState(nx, ny, nz);
        return BlockRenderShapeProvider.GetShape(neighbor) == BlockRenderShape.FullBlock;
    }

    //GetLightForFace gets the packed light coords at the face's outer neighbor position
    //When _lightSampler is null, returns FullBright for unit tests without a lighting environment
    private int GetLightForFace(int x, int y, int z, int originX, int originY, int originZ,
        Direction dir, int lightEmission)
    {
        if (_lightSampler is null) return LightTexture.FullBrightCoords;
        var offset = dir.UnitVector();
        var neighborX = originX + x + (int)offset.X;
        var neighborY = originY + y + (int)offset.Y;
        var neighborZ = originZ + z + (int)offset.Z;
        return _lightSampler.GetLightCoords(neighborX, neighborY, neighborZ, lightEmission);
    }

    //ApplyFaceShade bakes the face shade factor into Color's RGB segment, preserving Alpha
    //Vanilla tiers by axis: Y axis Up=1.0/Down=0.5, Z axis North/South=0.8, X axis East/West=0.6
    private static int ApplyFaceShade(int color, Direction dir)
    {
        var shade = FaceShade(dir);
        if (shade >= 1.0f) return color;
        var a = (color >> 24) & 0xFF;
        var r = (int)(((color >> 16) & 0xFF) * shade);
        var g = (int)(((color >> 8) & 0xFF) * shade);
        var b = (int)((color & 0xFF) * shade);
        return (a << 24) | (r << 16) | (g << 8) | b;
    }

    private static float FaceShade(Direction dir) => dir switch
    {
        Direction.Down => 0.5f,
        Direction.Up => 1.0f,
        Direction.North => 0.8f,
        Direction.South => 0.8f,
        Direction.West => 0.6f,
        Direction.East => 0.6f,
        _ => 1.0f
    };

    private static readonly Direction[] AllDirections =
    {
        Direction.Down, Direction.Up, Direction.North,
        Direction.South, Direction.West, Direction.East
    };
}
