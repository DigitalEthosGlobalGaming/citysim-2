namespace CitySim;

/// <summary>
/// One cell of the city grid.
/// </summary>
public class Tile
{
	public int X;
	public int Y;
	public Vector3 Center;

	public TileType Type;
	public Piece Piece;
	public string CardId;

	public GameObject Ground;
	public ModelRenderer GroundRenderer;
	public Color GroundBaseTint = new Color(0.85f, 1f, 0.8f);
	public GameObject Building;
	public ModelRenderer BuildingRenderer;

	public Guid Owner;

	/// <summary>True for tiles a player placed (as opposed to the generated starting streets).</summary>
	public bool ByPlayer;
	public int Order;
	public Color BaseTint = Color.White;

	// Drop-in animation.
	public float DropTime;

	public bool IsRoad => Type == TileType.Road;
}

public readonly record struct Cell( int X, int Y )
{
	public static Cell operator +( Cell a, Cell b ) => new( a.X + b.X, a.Y + b.Y );
}
