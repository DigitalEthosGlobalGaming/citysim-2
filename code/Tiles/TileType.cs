namespace CitySim;

// Legacy tile categories are only render-model choices. CardDef owns gameplay identity.
public enum TileType { Empty = 0, Park = 1, Business = 2, House = 3, Road = 4 }
public enum GameMode { Match = 0, Sandbox = 2 }
public enum GameState { Menu = 0, Playing = 1, End = 2 }
public struct Piece
{
	public TileType Type;
	public int BodyGroup;
	public int MaterialIndex;
	public bool IsEmpty => Type == TileType.Empty;
}
public static class TileRules
{
	public const string Empty = "models/roads/street_empty.vmdl";
	public const string Ghost = "models/roads/street_ghost.vmdl";
	public const string House = "models/buildings/house_01.vmdl";
	public const string Shop = "models/buildings/shop.vmdl";
	public const string Forest = "models/buildings/forest.vmdl";
	public static bool NeedsRoad(TileType type) => type is TileType.House or TileType.Business;
}
