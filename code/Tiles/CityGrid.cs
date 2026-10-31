namespace CitySim;

/// <summary>
/// The playfield. Owns every tile, spawns the models for them and answers neighbour / path queries.
/// </summary>
public sealed class CityGrid : Component
{
	/// <summary>World size of one cell. The old art is 200 units wide, scaled by <see cref="ModelScale"/>.</summary>
	public const float CellSize = 50f;
	public const float ModelScale = 0.25f;
	public const float SurfaceHeight = 3.75f;
	const float DropHeight = 25f;
	const float DropDuration = 0.35f;

	static readonly Color[] ShopTints =
	{
		new( 1f, 1f, 1f ), new( 1f, 0.9f, 0.75f ), new( 0.8f, 0.95f, 1f ), new( 1f, 0.85f, 0.9f ), new( 0.85f, 1f, 0.85f ),
	};

	// Up, Right, Down, Left - same ordering as the original game.
	public static readonly Cell[] Directions =
	{
		new( 0, -1 ), new( 1, 0 ), new( 0, 1 ), new( -1, 0 ),
	};

	public int Width { get; private set; }
	public int Height { get; private set; }
	public Tile[,] Tiles { get; private set; }

	public int EmptyCount { get; private set; }
	public int TotalCount => Width * Height;

	readonly List<Tile> dropping = new();
	bool generating;
	int placeCounter;

	public bool IsBuilt => Tiles != null;
	public int RoadRevision { get; private set; }

	public void Build( int width, int height, int seed = 0, bool generateStreets = true )
	{
		Clear();

		Width = width;
		Height = height;
		Tiles = new Tile[width, height];
		BuildBoard();

		var groundModel = GroundModel();

		for ( int x = 0; x < width; x++ )
		for ( int y = 0; y < height; y++ )
		{
			var tile = new Tile { X = x, Y = y, Center = CellToWorld( x, y ) };
			// Quiet variation keeps the grid legible without a high-contrast checkerboard.
			tile.GroundBaseTint = CardVisualVariation.Ground(null,x,y);

			tile.Ground = new GameObject( true, $"Ground {x},{y}" );
			tile.Ground.Parent = GameObject;
			tile.Ground.WorldPosition = tile.Center;
			tile.Ground.WorldScale = 1f;

			var renderer = tile.Ground.Components.Create<ModelRenderer>();
			renderer.Model = groundModel;
			tile.GroundRenderer = renderer;
			renderer.Tint = tile.GroundBaseTint;

			Tiles[x, y] = tile;
		}

		EmptyCount = width * height;
		generating = true;
		placeCounter = 0;
		if ( generateStreets )
			GenerateStreets( new Random( seed ) );
		generating = false;
	}

	/// <summary>A layered cardboard backing and printed dashes frame the playable cells.</summary>
	void BuildBoard()
	{
		var width = Width * CellSize;
		var height = Height * CellSize;
		const float rim = 30f;
		var layers = new Dictionary<Color, VertexBuffer>();
		void Box( float x, float y, float z, float sx, float sy, float sz, Color color )
		{
			if ( !layers.TryGetValue( color, out var vertices ) )
			{
				vertices = new VertexBuffer(); vertices.Init( true ); layers[color] = vertices;
			}
			vertices.AddCube( new Vector3( x, y, z ), new Vector3( sx, sy, sz ), Rotation.Identity, Color.White );
		}

		var ink = new Color( 0.165f, 0.129f, 0.094f );
		var core = new Color( 0.78f, 0.60f, 0.37f );
		var paper = new Color( 0.95f, 0.90f, 0.77f );
		// Each layer is a single rectangle, so the board also fills gaps between tiles.
		Box( 0, 0, -9f, width + rim * 2 + 4, height + rim * 2 + 4, 2, ink );
		Box( 0, 0, -5f, width + rim * 2, height + rim * 2, 6, core );
		Box( 0, 0, -1.5f, width + rim * 2, height + rim * 2, 1, paper );

		// Center each run independently so the corners stay tidy at any board size.
		void Dashes( float length, float offset, bool horizontal )
		{
			var count = Math.Max( 1, (int)(length / 24f) );
			var spacing = length / count;
			for ( var i = 0; i < count; i++ )
			{
				var along = -length * 0.5f + (i + 0.5f) * spacing;
				if ( horizontal ) Box( along, offset, -0.85f, spacing * 0.55f, 2.5f, 0.3f, ink );
				else Box( offset, along, -0.85f, 2.5f, spacing * 0.55f, 0.3f, ink );
			}
		}
		Dashes( width, height * 0.5f + rim * 0.5f, true );
		Dashes( width, -height * 0.5f - rim * 0.5f, true );
		Dashes( height, width * 0.5f + rim * 0.5f, false );
		Dashes( height, -width * 0.5f - rim * 0.5f, false );

		var board = new GameObject( true, "Cardboard board" ) { Parent = GameObject };
		board.WorldPosition = WorldPosition;
		foreach ( var layer in layers )
		{
			var mesh = new Mesh( Material.Load( "materials/board/board_flat.vmat" ) );
			mesh.CreateBuffers( layer.Value );
			var part = new GameObject( true, "Board layer" ) { Parent = board };
			part.LocalPosition = Vector3.Zero;
			var renderer = part.Components.Create<ModelRenderer>();
			renderer.Model = new ModelBuilder().AddMesh( mesh ).Create();
			renderer.Tint = layer.Key;
		}
	}

	static Model groundTileModel;
	static Model GroundModel()
	{
		if ( groundTileModel != null ) return groundTileModel;
		var vertices = new VertexBuffer(); vertices.Init( true );
		vertices.AddCube( Vector3.Up * (SurfaceHeight * 0.5f),
			new Vector3( CellSize - 0.6f, CellSize - 0.6f, SurfaceHeight ), Rotation.Identity, Color.White );
		var mesh = new Mesh( Material.Load( "materials/board/board_flat.vmat" ) );
		mesh.CreateBuffers( vertices );
		return groundTileModel = new ModelBuilder().AddMesh( mesh ).Create();
	}

	public void Clear()
	{
		RoadRevision++;
		foreach ( var child in GameObject.Children.ToList() )
			child.Destroy();

		Tiles = null;
		dropping.Clear();
		EmptyCount = 0;
	}

	/// <summary>
	/// Lay a couple of random roads across the map so there is always somewhere to start.
	/// </summary>
	void GenerateStreets( Random rng )
	{
		var rows = rng.Next( 1, 5 );
		var cols = rng.Next( 1, 5 );

		for ( int i = 0; i < rows; i++ )
		{
			var y = rng.Next( 0, Height );
			for ( int x = 0; x < Width; x++ )
				Place( x, y, new Piece { Type = TileType.Road }, animate: false );
		}

		for ( int i = 0; i < cols; i++ )
		{
			var x = rng.Next( 0, Width );
			for ( int y = 0; y < Height; y++ )
				Place( x, y, new Piece { Type = TileType.Road }, animate: false );
		}
	}

	public Vector3 CellToWorld( int x, int y )
	{
		var origin = WorldPosition - new Vector3( (Width - 1) * 0.5f, (Height - 1) * 0.5f, 0 ) * CellSize;
		return origin + new Vector3( x * CellSize, y * CellSize, 0 );
	}

	public Vector3 Size => new( Width * CellSize, Height * CellSize, 0 );

	public Tile Get( int x, int y )
	{
		if ( Tiles == null || x < 0 || y < 0 || x >= Width || y >= Height )
			return null;

		return Tiles[x, y];
	}

	public Tile Neighbour( Tile tile, int direction )
	{
		var d = Directions[direction];
		return Get( tile.X + d.X, tile.Y + d.Y );
	}

	/// <summary>
	/// Find which tile a world position falls inside, or null if it is off the map.
	/// </summary>
	public Tile WorldToTile( Vector3 world )
	{
		var local = world - CellToWorld( 0, 0 ) + new Vector3( CellSize, CellSize, 0 ) * 0.5f;
		return Get( (local.x / CellSize).FloorToInt(), (local.y / CellSize).FloorToInt() );
	}

	// ---------------------------------------------------------------- placement

	public bool CanPlace( Tile tile, Piece piece )
	{
		if ( tile == null || piece.IsEmpty || tile.Type != TileType.Empty )
			return false;

		if ( TileRules.NeedsRoad( piece.Type ) )
			return RoadNeighbourDirection( tile ) >= 0;

		return true;
	}

	public bool Place( int x, int y, Piece piece, bool animate = true, bool legacyRules = true )
	{
		var tile = Get( x, y );
		if ( legacyRules ? !CanPlace( tile, piece ) : tile == null || piece.IsEmpty || tile.Type != TileType.Empty )
			return false;

		tile.Type = piece.Type;
		if (piece.Type == TileType.Road) RoadRevision++;
		tile.Piece = piece;
		tile.ByPlayer = !generating;
		tile.Order = ++placeCounter;
		EmptyCount--;

		SpawnBuilding( tile, animate );

		// Roads next to us need to re-shape themselves.
		for ( int i = 0; i < 4; i++ )
		{
			var n = Neighbour( tile, i );
			if ( n != null && n.IsRoad )
				RefreshRoad( n );
		}

		return true;
	}

	internal bool ClearBuilding(int x,int y)
	{
		var tile=Get(x,y); if (tile == null || tile.Type==TileType.Empty) return false;
		if (tile.IsRoad) RoadRevision++;
		dropping.Remove(tile);
		if (tile.Building.IsValid()) tile.Building.Destroy();
		tile.Building=null; tile.BuildingRenderer=null; tile.Type=TileType.Empty; tile.Piece=default;
		tile.CardId=null; tile.Owner=Guid.Empty; tile.Order=0; tile.ByPlayer=false; tile.BaseTint=Color.White; tile.DropTime=0;
		tile.GroundBaseTint=CardVisualVariation.Ground(null,x,y);
		if (tile.GroundRenderer.IsValid()) tile.GroundRenderer.Tint=tile.GroundBaseTint;
		if (tile.Ground.IsValid()) tile.Ground.Enabled=true;
		EmptyCount++; return true;
	}
	void SpawnBuilding( Tile tile, bool animate )
	{
		var piece = tile.Piece;

		tile.Building = new GameObject( true, $"{piece.Type} {tile.X},{tile.Y}" );
		tile.Building.Parent = GameObject;
		tile.Building.WorldScale = ModelScale;

		var renderer = tile.Building.Components.Create<ModelRenderer>();
		tile.BuildingRenderer = renderer;

		switch ( piece.Type )
		{
			case TileType.Road:
				RefreshRoad( tile );
				break;

			case TileType.House:
				renderer.Model = Model.Load( TileRules.House );
				renderer.SetBodyGroup( "base", piece.BodyGroup );
				ApplyMaterialGroup( renderer, piece.MaterialIndex );
				FaceRoad( tile );
				break;

			case TileType.Business:
				renderer.Model = Model.Load( TileRules.Shop );
				renderer.SetBodyGroup( "base", piece.BodyGroup );
				tile.BaseTint = ShopTints[piece.MaterialIndex % ShopTints.Length];
				renderer.Tint = tile.BaseTint;
				FaceRoad( tile );
				break;

			case TileType.Park:
				renderer.Model = Model.Load( TileRules.Forest );
				tile.Building.WorldRotation = Rotation.Identity;
				break;
		}

		// Roads and parks replace the ground tile entirely.
		if ( piece.Type == TileType.Road || piece.Type == TileType.Park )
			tile.Ground.Enabled = false;

		if ( animate )
		{
			tile.DropTime = DropDuration;
			tile.Building.WorldPosition = tile.Center + Vector3.Up * DropHeight;
			dropping.Add( tile );
		}
		else
		{
			tile.Building.WorldPosition = tile.Center;
		}
	}

	public static void ApplyMaterialGroup( ModelRenderer renderer, int index )
	{
		var model = renderer.Model;
		if ( model == null || model.MaterialGroupCount == 0 )
			return;

		renderer.MaterialGroup = model.GetMaterialGroupName( index % model.MaterialGroupCount );
	}

	protected override void OnUpdate()
	{
		for ( int i = dropping.Count - 1; i >= 0; i-- )
		{
			var tile = dropping[i];
			if ( tile.Building == null || !tile.Building.IsValid() )
			{
				dropping.RemoveAt( i );
				continue;
			}

			tile.DropTime -= Time.Delta;
			var t = 1f - MathX.Clamp( tile.DropTime / DropDuration, 0f, 1f );
			tile.Building.WorldPosition = tile.Center + Vector3.Up * (DropHeight * ((1f - t) * (1f - t)));

			if ( tile.DropTime <= 0 )
			{
				tile.Building.WorldPosition = tile.Center;
				dropping.RemoveAt( i );
			}
		}
	}

	// ---------------------------------------------------------------- neighbours

	/// <summary>
	/// Direction index (see <see cref="Directions"/>) of the first road next to this tile, or -1.
	/// </summary>
	public int RoadNeighbourDirection( Tile tile )
	{
		for ( int i = 0; i < 4; i++ )
		{
			if ( Neighbour( tile, i )?.IsRoad == true )
				return i;
		}

		return -1;
	}

	public bool IsNextToRoad( Tile tile ) => RoadNeighbourDirection( tile ) >= 0;

	public static float YawForDirection( int direction ) => direction switch
	{
		0 => 270f,
		1 => 0f,
		2 => 90f,
		_ => 180f,
	};

	void FaceRoad( Tile tile )
	{
		var dir = RoadNeighbourDirection( tile );
		if ( dir >= 0 )
			tile.Building.WorldRotation = Rotation.FromYaw( YawForDirection( dir ) );
	}

	// ---------------------------------------------------------------- roads

	/// <summary>
	/// Works out which road model and rotation a tile at this position needs, based on its neighbours.
	/// Off-map neighbours count as connected so streets run off the edge of the map.
	/// </summary>
	public (string model, float yaw) RoadShape( int x, int y )
	{
		bool Connected( int dx, int dy )
		{
			var n = Get( x + dx, y + dy );
			return n == null || n.IsRoad;
		}

		var up = Connected( 0, -1 );
		var right = Connected( 1, 0 );
		var down = Connected( 0, 1 );
		var left = Connected( -1, 0 );

		var count = (up ? 1 : 0) + (right ? 1 : 0) + (down ? 1 : 0) + (left ? 1 : 0);
		float yaw = 0;

		switch ( count )
		{
			case 3:
				if ( !up ) yaw = 180;
				else if ( !right ) yaw = 270;
				else if ( !down ) yaw = 0;
				else yaw = 90;
				return ("models/kenney/roads/road-intersection.vmdl", yaw - 90);

			case 2:
				if ( left && right )
					return ("models/kenney/roads/road-straight.vmdl", 90);
				if ( up && down )
					return ("models/kenney/roads/road-straight.vmdl", 0);

				if ( up ) yaw = left ? 180 : 270;
				if ( down ) yaw = left ? 90 : 0;
				return ("models/kenney/roads/road-bend.vmdl", yaw + 90);

			case 1:
				if ( up ) yaw = 0;
				else if ( right ) yaw = 90;
				else if ( down ) yaw = 180;
				else yaw = 270;
				return ("models/kenney/roads/road-end.vmdl", yaw + 180);

			default:
				return ("models/kenney/roads/road-crossroad.vmdl", 0);
		}
	}

	internal void RefreshRoad( Tile tile )
	{
		if ( tile.BuildingRenderer == null )
			return;

		var (model, yaw) = RoadShape( tile.X, tile.Y );
		if (tile.CardId == "avenue" && model.EndsWith("road-crossroad.vmdl")) model = "models/kenney/roads/road-crossroad-line.vmdl";
		tile.BuildingRenderer.Enabled = false;
		CardVisuals.BuildRoad(tile.Building,model,tile.CardId == "roundabout");
		tile.BaseTint = tile.CardId == "avenue" ? new Color(0.8f,0.85f,1f) : Color.White;
		CardVisuals.Tint(tile.Building,tile.BaseTint);
		tile.Building.WorldRotation = Rotation.FromYaw( yaw );
	}

	// ---------------------------------------------------------------- pathfinding

	/// <summary>
	/// Breadth first search across roads from one tile to another. Returns the road tiles to drive over, or null.
	/// </summary>
	public List<Tile> FindRoadPath( Tile from, Tile to )
	{
		if ( from == null || to == null || !from.IsRoad || !to.IsRoad )
			return null;

		var previous = new Dictionary<Tile, Tile> { [from] = null };
		var queue = new Queue<Tile>();
		queue.Enqueue( from );

		while ( queue.Count > 0 )
		{
			var current = queue.Dequeue();
			if ( current == to )
				break;

			for ( int i = 0; i < 4; i++ )
			{
				var n = Neighbour( current, i );
				if ( n == null || !n.IsRoad || previous.ContainsKey( n ) )
					continue;

				previous[n] = current;
				queue.Enqueue( n );
			}
		}

		if ( !previous.ContainsKey( to ) )
			return null;

		var path = new List<Tile>();
		for ( var t = to; t != null; t = previous[t] )
			path.Add( t );

		path.Reverse();
		return path;
	}

	/// <summary>
	/// Road tiles touching the given tile.
	/// </summary>
	public IEnumerable<Tile> AdjacentRoads( Tile tile )
	{
		for ( int i = 0; i < 4; i++ )
		{
			var n = Neighbour( tile, i );
			if ( n != null && n.IsRoad )
				yield return n;
		}
	}

	public IEnumerable<Tile> All()
	{
		if ( Tiles == null )
			yield break;

		for ( int x = 0; x < Width; x++ )
		for ( int y = 0; y < Height; y++ )
			yield return Tiles[x, y];
	}

	public IEnumerable<Tile> EdgeRoads()
	{
		return All().Where( t => t.IsRoad && (t.X == 0 || t.Y == 0 || t.X == Width - 1 || t.Y == Height - 1) );
	}
}
