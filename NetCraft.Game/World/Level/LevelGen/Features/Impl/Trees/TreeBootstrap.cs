namespace NetCraft.Game.World.Level.LevelGen.Features.Impl.Trees;

//TreeBootstrap 树木体系注册入口
//触碰全部类型单例与 tree 特征单例 触发静态注册把注册表填满
//注册表要先于数据加载填好 否则 worldgen 里 tree 相关 JSON 的 type 字段整批解不出来
public static class TreeBootstrap
{
    public static void RegisterAll()
    {
        //树干放置器类型
        _ = TrunkPlacerTypes.Straight;
        _ = TrunkPlacerTypes.Forking;
        _ = TrunkPlacerTypes.Giant;
        _ = TrunkPlacerTypes.MegaJungle;
        _ = TrunkPlacerTypes.DarkOak;
        _ = TrunkPlacerTypes.Fancy;
        _ = TrunkPlacerTypes.Bending;
        _ = TrunkPlacerTypes.UpwardsBranching;
        _ = TrunkPlacerTypes.Cherry;

        //树叶放置器类型
        _ = FoliagePlacerTypes.Blob;
        _ = FoliagePlacerTypes.Spruce;
        _ = FoliagePlacerTypes.Pine;
        _ = FoliagePlacerTypes.Acacia;
        _ = FoliagePlacerTypes.DarkOak;
        _ = FoliagePlacerTypes.MegaPine;
        _ = FoliagePlacerTypes.Jungle;
        _ = FoliagePlacerTypes.Fancy;
        _ = FoliagePlacerTypes.Bush;
        _ = FoliagePlacerTypes.RandomSpread;
        _ = FoliagePlacerTypes.Cherry;

        //树根放置器类型
        _ = RootPlacerTypes.Mangrove;

        //特征尺寸类型
        _ = FeatureSizeTypes.TwoLayers;
        _ = FeatureSizeTypes.ThreeLayers;

        //树木装饰器类型
        _ = TreeDecoratorTypes.TrunkVine;
        _ = TreeDecoratorTypes.LeaveVine;
        _ = TreeDecoratorTypes.Cocoa;
        _ = TreeDecoratorTypes.Beehive;
        _ = TreeDecoratorTypes.AlterGround;
        _ = TreeDecoratorTypes.AttachedToLeaves;
        _ = TreeDecoratorTypes.PlaceOnGround;
        _ = TreeDecoratorTypes.PaleMoss;
        _ = TreeDecoratorTypes.CreakingHeart;

        //树特征本身
        _ = TreeFeature.Instance;
    }
}
