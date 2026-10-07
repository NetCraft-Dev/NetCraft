namespace NetCraft.Game.DFU.Fixes;

using NetCraft.DataFixer.Fixes;

using NetCraft.DataFixer;

//A collection of game business literal constants, replacing the TAG_*/Fields.* scattered across the vanilla business classes
//Uses literal values directly as needed to avoid a NetCraft.Game dependency
public static class FixConstants
{
    public const string LivingEntityAttributes = "attributes";
    public const string LivingEntityBrain = "Brain";
    public const string EntityUuid = "UUID";
    public const string StateHolderName = "Name";
    public const string StateHolderProperties = "Properties";
    public const string MobDropChances = "DropChances";
    public const string ItemInstanceComponents = "components";
    public const string ItemInstanceCount = "Count";
    public const string JigsawBlockEntityName = "name";
    public const string JigsawBlockEntityTarget = "target";
    public const string DecoratedPotBlockEntityItem = "item";
    public const string DecoratedPotBlockEntitySherds = "sherds";
    public const string JukeboxBlockEntityTicksSinceSongStarted = "ticks_since_song_started";
    public const string ChunkRegionIoEventType = "type";
    public const string ChunkRegionIoEventDimension = "dimension";
    public const string PartNameFeet = "feet";
    public const string PartNameHead = "head";
    public const string PartNameBody = "body";
    public const string StructureTemplateBlocks = "blocks";
    public const string ContainerHelperItems = "Items";
    public const string ServerPlayerEnderPearls = "ender_pearls";
    public const string ServerRecipeBookRecipeBook = "recipe_book";
    public const string InventoryCarrierInventory = "Inventory";
    public const string PlayerRootVehicle = "RootVehicle";
    public const string PlayerEnderItems = "EnderItems";
    public const string PlayerShoulderEntityLeft = "ShoulderEntityLeft";
    public const string PlayerShoulderEntityRight = "ShoulderEntityRight";
    public const string RecipeBookRecipes = "recipes";
    public const string RecipeBookToBeDisplayed = "toBeDisplayed";
    public const string WrittenBookPages = "pages";
    public const string WrittenBookRaw = "raw";
    public const string WrittenBookFiltered = "filtered";
    public const string FoodUsingConvertsTo = "using_converts_to";
    public const string EntityItem = "Item";
}
