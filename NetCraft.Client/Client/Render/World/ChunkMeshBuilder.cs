using NetCraft.Game.Client.Render.Model;
using NetCraft.Gpu;
using NetCraft.Registry.State;
using NetCraft.Storage.Chunk;

namespace NetCraft.Game.Client.Render.World;

//ChunkMeshBuilder 区块 mesh 生成器对标原版 SectionCompiler
//遍历 LevelChunkSection 16³ 方块对非空方块查 BakedModel 按 cullface+邻居 BlockRenderShape 剔除面
//cullface quad 邻居是 FullBlock 则剔除该方向面 no-cull quad 总是渲染
//顶点位置由 BlockModelBaker 产出 0-16 范围经 PoseStack scale(1/16) 归一化到 0-1 再 translate(x,y,z) 平移到方块位置
//originX/Y/Z 是 section 世界基坐标光照查询用首版不跨 section 边界邻居查询超出 0-15 视为 air 不剔除
//光照通过 ChunkLightSampler 查面外侧邻居的 block/sky light null 时走 FullBright 兼容旧测试
//face-based shading 按 quad.Direction 固定 shade 系数 bake 进 Color RGB 对齐原版 face 拣选
//流体渲染不含 FluidRenderer 是独立子系统后续补
public sealed class ChunkMeshBuilder
{
    private readonly Func<BlockState, BakedModel?> _modelMapper;
    private readonly ChunkLightSampler? _lightSampler;

    public ChunkMeshBuilder(BlockStateModelMapper modelMapper, ChunkLightSampler? lightSampler = null)
        : this(modelMapper.GetModel, lightSampler) { }

    //Func 构造供测试注入 stub 映射避免依赖 ResourceManager
    public ChunkMeshBuilder(Func<BlockState, BakedModel?> modelMapper, ChunkLightSampler? lightSampler = null)
    {
        _modelMapper = modelMapper;
        _lightSampler = lightSampler;
    }

    //Build 遍历 section 16³ 方块生成 mesh 数据 originX/Y/Z 是 section 世界基坐标
    //regionCache=null 走 W7 越界视为 air 逻辑兼容旧单测
    public ChunkMeshData Build(LevelChunkSection section, int originX = 0, int originY = 0, int originZ = 0)
        => BuildCore(section, null, originX, originY, originZ);

    //Build 重载接收 RenderRegionCache 跨 section 邻居查询解决边界面剔除
    //regionCache.Center 应与 section 所在 SectionPos 一致
    public ChunkMeshData Build(LevelChunkSection section, RenderRegionCache regionCache, int originX, int originY, int originZ)
        => BuildCore(section, regionCache, originX, originY, originZ);

    //BuildBlock 把单个方块的模型按给定变换烘进 mesh 供活塞这类会移动的方块每帧重画
    //区块网格是静态烘焙的 每帧都变的位移烘不进去 只能单独走一条动态通道
    //移动中的方块邻居关系随时在变 六个方向的面一律不剔 光照按方块所在格取
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

    //EmitMovingQuads 写一批移动方块的 quad 光照取方块自身所在格
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

    //BuildCore 公共编译逻辑 regionCache!=null 时跨 section 面剔除 否则走 W7 越界视为 air
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
            //translate 加 sectionOrigin 把顶点 bake 到世界坐标 shader 端 Model=Identity
            pose.Translate(x + originX, y + originY, z + originZ);
            AddBlockQuads(pose, section, regionCache, x, y, z, originX, originY, originZ, model, mesh);
            pose.PopPose();
        }
        return mesh;
    }

    //AddBlockQuads 把方块的 BakedModel quad 按 layer + cullface 写入 mesh
    private void AddBlockQuads(PoseStack pose, LevelChunkSection section, RenderRegionCache? regionCache,
        int x, int y, int z, int originX, int originY, int originZ, BakedModel model, ChunkMeshData mesh)
    {
        var instance = new QuadInstance();
        foreach (var layer in model.Layers)
        {
            //cullface quad：按方向查邻居 FullBlock 则剔除
            AddCullfaceQuads(pose, section, regionCache, x, y, z, originX, originY, originZ, model, layer, mesh, instance);
            //no-cull quad：总是渲染
            AddNoCullQuads(pose, x, y, z, originX, originY, originZ, model, layer, mesh, instance);
        }
    }

    //AddCullfaceQuads 遍历 6 方向 cullface quad 邻居是 FullBlock 则跳过
    //regionCache!=null 跨 section 查邻居 否则走 W7 越界视为 air
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

    //AddNoCullQuads 写入无 cullface 的 quad 总是渲染
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

    //ShouldCullFace 判断当前方块某方向面是否被邻居遮挡应剔除
    //regionCache!=null 跨 section 查邻居世界坐标 否则走 W7 越界视为 air 不剔除
    //邻居 BlockRenderShape==FullBlock 则剔除 Custom/Empty 不剔除
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

    //GetLightForFace 查面外侧邻居位置的 packed light coords
    //_lightSampler 为 null 时返回 FullBright 兼容无光照环境的单测
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

    //ApplyFaceShade 把 face shade 系数 bake 进 Color 的 RGB 段保留 Alpha
    //原版按轴分档 Y 轴 Up=1.0/Down=0.5 Z 轴 North/South=0.8 X 轴 East/West=0.6
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
