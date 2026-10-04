using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage.Chunk;

namespace NetCraft.Storage.Light;

//SkyLightSectionStorage 天光区段存储对应原版 net.minecraft.world.level.lighting.SkyLightSectionStorage
//除层数据外还按列维护最高非空区段 topSections 位于其上的区段天光恒为 15
public class SkyLightSectionStorage : LayerLightSectionStorage<SkyLightSectionStorage.SkyDataLayerStorageMap>
{
    public SkyLightSectionStorage(LightChunkGetter chunkSource)
        : base(LightLayer.Sky, chunkSource, new SkyDataLayerStorageMap(int.MaxValue))
    {
    }

    protected internal override int GetLightValue(long blockNode) => GetLightValue(blockNode, false);

    //getLightValue 天光查询 区段在最高非空区段之上时直接是满值 15
    protected internal int GetLightValue(long blockNode, bool updating)
    {
        var sectionNode = SectionPos.BlockToSection(blockNode);
        var sectionY = SectionPos.GetY(sectionNode);
        var sections = updating ? UpdatingSectionData : VisibleSectionData;
        var topSection = sections.GetTopSectionY(SectionPos.GetZeroNode(sectionNode));
        if (topSection == sections.CurrentLowestY || sectionY >= topSection)
        {
            if (updating && !LightOnInSection(sectionNode)) return 0;
            return 15;
        }

        var layer = GetDataLayer(sections, sectionNode);
        if (layer is null)
        {
            //该区段没有层数据说明它在实体方块之上 沿列向上找第一个有数据的区段
            blockNode = BlockPos.GetFlatIndex(blockNode);
            while (layer is null)
            {
                sectionY++;
                if (sectionY >= topSection) return 15;
                sectionNode = SectionPos.Offset(sectionNode, Direction.Up);
                layer = GetDataLayer(sections, sectionNode);
            }
        }

        return layer.Get(
            SectionPos.SectionRelative(BlockPos.GetX(blockNode)),
            SectionPos.SectionRelative(BlockPos.GetY(blockNode)),
            SectionPos.SectionRelative(BlockPos.GetZ(blockNode)));
    }

    protected override void OnNodeAdded(long sectionNode)
    {
        var y = SectionPos.GetY(sectionNode);
        if (UpdatingSectionData.CurrentLowestY > y) UpdatingSectionData.CurrentLowestY = y;

        var zeroNode = SectionPos.GetZeroNode(sectionNode);
        if (UpdatingSectionData.GetTopSectionY(zeroNode) < y + 1)
            UpdatingSectionData.SetTopSectionY(zeroNode, y + 1);
    }

    protected override void OnNodeRemoved(long sectionNode)
    {
        var zeroNode = SectionPos.GetZeroNode(sectionNode);
        var y = SectionPos.GetY(sectionNode);
        if (UpdatingSectionData.GetTopSectionY(zeroNode) != y + 1) return;

        //自该区段向下找第一个仍在存光的区段作为新的最高非空区段
        var current = sectionNode;
        while (!StoringLightForSection(current) && HasLightDataAtOrBelow(y))
        {
            y--;
            current = SectionPos.Offset(current, Direction.Down);
        }

        if (StoringLightForSection(current)) UpdatingSectionData.SetTopSectionY(zeroNode, y + 1);
        else UpdatingSectionData.RemoveTopSection(zeroNode);
    }

    protected override DataLayer CreateDataLayer(long sectionNode)
    {
        if (QueuedSections.TryGetValue(sectionNode, out var queuedLayer)) return queuedLayer;

        var sections = UpdatingSectionData;
        var topSection = sections.GetTopSectionY(SectionPos.GetZeroNode(sectionNode));
        if (topSection == sections.CurrentLowestY || SectionPos.GetY(sectionNode) >= topSection)
            return LightOnInSection(sectionNode) ? new DataLayer(15) : new DataLayer();

        //在最高非空区段之下时 取上一个有数据的区段并把它最底一层复制到本区段
        var above = SectionPos.Offset(sectionNode, Direction.Up);
        while (true)
        {
            var aboveData = GetDataLayer(above, true);
            if (aboveData is null) above = SectionPos.Offset(above, Direction.Up);
            else return RepeatFirstLayer(aboveData);
        }
    }

    //repeatFirstLayer 把层数据里的第一层复制到 16 层 用于表示"整段天光相同"
    private static DataLayer RepeatFirstLayer(DataLayer data)
    {
        if (data.IsDefinitelyHomogenous) return data.Copy();
        var input = data.GetData();
        var output = new byte[DataLayer.Size];
        for (var i = 0; i < 16; i++) Array.Copy(input, 0, output, i * DataLayer.LayerSize, DataLayer.LayerSize);
        return new DataLayer(output);
    }

    protected internal bool HasLightDataAtOrBelow(int sectionY) => sectionY >= UpdatingSectionData.CurrentLowestY;

    protected internal bool IsAboveData(long sectionNode)
    {
        var sections = UpdatingSectionData;
        var topSection = sections.GetTopSectionY(SectionPos.GetZeroNode(sectionNode));
        return topSection == sections.CurrentLowestY || SectionPos.GetY(sectionNode) >= topSection;
    }

    protected internal int GetTopSectionY(long zeroNode) => UpdatingSectionData.GetTopSectionY(zeroNode);

    protected internal int GetBottomSectionY() => UpdatingSectionData.CurrentLowestY;

    //SkyDataLayerStorageMap 天光层映射对应原版同名嵌套类
    //topSections 缺失时返回值对齐原版 defaultReturnValue(currentLowestY)
    public sealed class SkyDataLayerStorageMap : DataLayerStorageMap<SkyDataLayerStorageMap>
    {
        private readonly Dictionary<long, int> _topSections;

        public int CurrentLowestY { get; set; }

        public SkyDataLayerStorageMap(int currentLowestY)
        {
            _topSections = new Dictionary<long, int>();
            CurrentLowestY = currentLowestY;
        }

        private SkyDataLayerStorageMap(Dictionary<long, DataLayer> map, Dictionary<long, int> topSections, int currentLowestY)
            : base(map)
        {
            _topSections = topSections;
            CurrentLowestY = currentLowestY;
        }

        public int GetTopSectionY(long zeroNode)
            => _topSections.TryGetValue(zeroNode, out var value) ? value : CurrentLowestY;

        public void SetTopSectionY(long zeroNode, int sectionY) => _topSections[zeroNode] = sectionY;

        public void RemoveTopSection(long zeroNode) => _topSections.Remove(zeroNode);

        public override SkyDataLayerStorageMap Copy()
            => new(CopyEntries(), new Dictionary<long, int>(_topSections), CurrentLowestY);
    }
}
