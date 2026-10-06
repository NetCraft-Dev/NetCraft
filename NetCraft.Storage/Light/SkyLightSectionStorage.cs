using NetCraft.Primitives;
using NetCraft.Registry;
using NetCraft.Storage.Chunk;

namespace NetCraft.Storage.Light;

//SkyLightSectionStorage, sky light section storage, maps to vanilla net.minecraft.world.level.lighting.SkyLightSectionStorage
//Besides layer data it maintains the highest non-empty section per column (topSections); sections above it are always sky light 15
public class SkyLightSectionStorage : LayerLightSectionStorage<SkyLightSectionStorage.SkyDataLayerStorageMap>
{
    public SkyLightSectionStorage(LightChunkGetter chunkSource)
        : base(LightLayer.Sky, chunkSource, new SkyDataLayerStorageMap(int.MaxValue))
    {
    }

    protected internal override int GetLightValue(long blockNode) => GetLightValue(blockNode, false);

    //getLightValue, sky light query; when the section is above the highest non-empty one it is directly full, 15
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
            //A missing layer here means the section is above solid blocks; walk up the column to the first section with data
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

        //Walk down from that section to the first one still storing light as the new highest non-empty section
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

        //When below the highest non-empty section, take the section above with data and copy its bottom layer into this one
        var above = SectionPos.Offset(sectionNode, Direction.Up);
        while (true)
        {
            var aboveData = GetDataLayer(above, true);
            if (aboveData is null) above = SectionPos.Offset(above, Direction.Up);
            else return RepeatFirstLayer(aboveData);
        }
    }

    //repeatFirstLayer copies the first layer of the layer data to all 16 layers, meaning "the whole section has the same sky light"
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

    //SkyDataLayerStorageMap, sky light layer map, maps to the vanilla nested class of the same name
    //When topSections is missing the return aligns with vanilla defaultReturnValue(currentLowestY)
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
