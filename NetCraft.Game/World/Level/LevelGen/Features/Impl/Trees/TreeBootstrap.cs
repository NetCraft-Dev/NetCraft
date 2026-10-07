namespace NetCraft.Game.World.Level.LevelGen.Features.Impl.Trees;

//TreeBootstrap tree system registration entry
//Touching all type singletons and the tree feature singleton triggers static registration and fills the registries
//The registries must be populated before data loading, otherwise the type fields of tree-related worldgen JSON cannot be decoded at all
public static class TreeBootstrap
{
    public static void RegisterAll()
    {
        //Trunk placer types
        _ = TrunkPlacerTypes.Straight;
        _ = TrunkPlacerTypes.Forking;
        _ = TrunkPlacerTypes.Giant;
        _ = TrunkPlacerTypes.MegaJungle;
        _ = TrunkPlacerTypes.DarkOak;
        _ = TrunkPlacerTypes.Fancy;
        _ = TrunkPlacerTypes.Bending;
        _ = TrunkPlacerTypes.UpwardsBranching;
        _ = TrunkPlacerTypes.Cherry;

        //Foliage placer types
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

        //Root placer types
        _ = RootPlacerTypes.Mangrove;

        //Feature size types
        _ = FeatureSizeTypes.TwoLayers;
        _ = FeatureSizeTypes.ThreeLayers;

        //Tree decorator types
        _ = TreeDecoratorTypes.TrunkVine;
        _ = TreeDecoratorTypes.LeaveVine;
        _ = TreeDecoratorTypes.Cocoa;
        _ = TreeDecoratorTypes.Beehive;
        _ = TreeDecoratorTypes.AlterGround;
        _ = TreeDecoratorTypes.AttachedToLeaves;
        _ = TreeDecoratorTypes.PlaceOnGround;
        _ = TreeDecoratorTypes.PaleMoss;
        _ = TreeDecoratorTypes.CreakingHeart;

        //The tree feature itself
        _ = TreeFeature.Instance;
    }
}
