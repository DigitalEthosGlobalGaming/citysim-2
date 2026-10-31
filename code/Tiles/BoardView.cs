using CitySim.Cards;
using CitySim.Match;

namespace CitySim;

/// <summary>
/// Additive scene projection of a match. Rules validate first; this view only renders results.
/// Rebinding rebuilds the view from model cells and detaches the previous match's notifications.
/// </summary>
public sealed class BoardView : Component
{
	MatchState match;
	CityGrid grid;
	readonly List<(Tile Tile,float From,float To,float Age)> pops = new();
	readonly List<GameObject> markers = new();
	readonly Dictionary<(int X,int Y),GameObject> ownershipFrames = new();
	static readonly Dictionary<int,Model> ownershipModels = new();
	Tag highlighted;
	Guid highlightOwner;
	Guid? playerHighlight;
	IReadOnlyList<InteractionTarget> interactionTargets;
	readonly List<BoardCell> highlightedCells = new();
	readonly Dictionary<(int X,int Y),(GameObject Object,float Age,bool Retiring)> outlines = new();
	static Model outlineModel;
	IReadOnlyList<PlacementInteraction> placementInteractions = Array.Empty<PlacementInteraction>();
	public IReadOnlyList<BoardCell> HighlightedCells => highlightedCells;
	public MatchState Match => match;

	public void Bind( MatchState state, CityGrid target )
	{
		if ( state == null || !target.IsValid() ) throw new System.ArgumentException("A match and grid are required.");
		Unbind();
		match = state;
		grid = target;
		grid.Build(state.Width,state.Height,state.Seed,generateStreets:false);
		foreach ( var cell in state.Cells().OrderBy(c => c.Order) ) Render(cell,false);
		match.Events.CardPlaced += OnPlaced;
		match.Events.BuildingReplaced += Replaced;
		match.Events.CellUpdated += Updated;
		match.Events.ParkScored += PopPark;
		match.Events.ClaimsChanged += Claims;
		Claims();
	}

	public void Unbind()
	{
		if ( match != null ) { match.Events.CardPlaced -= OnPlaced; match.Events.BuildingReplaced -= Replaced; match.Events.CellUpdated -= Updated; match.Events.ParkScored -= PopPark; match.Events.ClaimsChanged -= Claims; }
		pops.Clear(); ClearOutlines(); foreach (var frame in ownershipFrames.Values) if (frame.IsValid()) frame.Destroy(); ownershipFrames.Clear(); highlightedCells.Clear(); foreach (var marker in markers) if (marker.IsValid()) marker.Destroy(); markers.Clear();
		highlighted = Tag.None; interactionTargets = null; playerHighlight = null; placementInteractions = Array.Empty<PlacementInteraction>();
		match = null;
		grid = null;
	}
	protected override void OnDestroy() => Unbind();
	void OnPlaced( BoardCell cell ) { Render(cell,true); if (highlighted != Tag.None || interactionTargets != null || playerHighlight != null) RefreshHighlight(); }
	void Replaced(BoardReplacement change)
	{
		var tile=grid?.Get(change.Before.X,change.Before.Y);
		if (tile == null) return;
		pops.RemoveAll(p => p.Tile==tile); grid.ClearBuilding(tile.X,tile.Y);
		if (ownershipFrames.Remove((tile.X,tile.Y),out var frame) && frame.IsValid()) frame.Destroy();
		if (highlighted != Tag.None || interactionTargets != null || playerHighlight != null) RefreshHighlight();
	}
	static float Scale(BoardCell cell) => cell.CardId == "sapling" && !Growth.IsMature(cell) ? 0.55f : 1f;
	void Updated(BoardCell cell)
	{
		var tile = grid?.Get(cell.X,cell.Y);
		if (tile?.Building == null || !tile.Building.IsValid()) return;
		if (cell.CardId != "sapling") return;
		var target = Scale(cell);
		CardVisuals.Build(tile.Building,cell.CardId,Growth.IsMature(cell),CardVisualVariation.Seed(cell.CardId,cell.X,cell.Y)); Tint(cell);
		tile.Building.Name = $"{(cell.CardId == "sapling" && Growth.IsMature(cell) ? "Forest" : CardLibrary.Get(cell.CardId).Name)} {cell.X},{cell.Y}";
		if (MathF.Abs(tile.Building.WorldScale.z/CityGrid.ModelScale-target) > 0.01f) { pops.RemoveAll(p => p.Tile == tile); pops.Add((tile,tile.Building.WorldScale.z/CityGrid.ModelScale,target,0)); }
	}
	void PopPark(ParkScoreResult park)
	{
		foreach (var cell in park.Cells)
		{
			var tile = grid.Get(cell.X,cell.Y); if (tile?.Building == null) continue;
			var target = Scale(cell); pops.RemoveAll(p => p.Tile == tile);
			pops.Add((tile,cell.CardId == "sapling" && Growth.IsMature(cell) ? tile.Building.WorldScale.z/CityGrid.ModelScale : target*0.8f,target,0));
		}
	}
	void Claims()
	{
		foreach (var marker in markers) if (marker.IsValid()) marker.Destroy(); markers.Clear();
		foreach (var claim in match.Claims)
		{
			var marker = new GameObject(true,$"Reserved {claim.X},{claim.Y}") { Parent = GameObject };
			marker.WorldPosition = grid.CellToWorld(claim.X,claim.Y)+Vector3.Up*1; marker.WorldScale = CityGrid.ModelScale;
			var renderer = marker.Components.Create<ModelRenderer>(); renderer.Model = Model.Load(TileRules.Ghost);
			renderer.Tint = claim.Owner == CityGame.Me ? new Color(0.4f,0.8f,1) : new Color(1,0.6f,0.3f); markers.Add(marker);
		}
	}
	public void Highlight(Tag tag,Guid owner)
	{
		highlighted = tag; highlightOwner = owner; interactionTargets = null;
		if (match == null || !grid.IsValid()) return;
		RefreshHighlight();
	}
	public void HighlightInteractions(IReadOnlyList<InteractionTarget> targets,Guid viewer)
	{
		highlighted = Tag.None; highlightOwner = viewer; interactionTargets = targets;
		if (match == null || !grid.IsValid()) return;
		RefreshHighlight();
	}
	public void HighlightPlayer(Guid? owner)
	{
		playerHighlight = owner;
		if (match != null && grid.IsValid()) RefreshHighlight();
	}
	public void PreviewInteractions(IReadOnlyList<PlacementInteraction> interactions)
	{
		placementInteractions = interactions ?? Array.Empty<PlacementInteraction>();
		if (match != null && grid.IsValid()) RefreshHighlight();
	}
	void RefreshHighlight()
	{
		highlightedCells.Clear();
		for (var x = 0; x < grid.Width; x++)
		for (var y = 0; y < grid.Height; y++)
		{
			var cell=match.Get(x,y);
			if (Matches(cell) || placementInteractions.Any(i => i.Cell.X == x && i.Cell.Y == y)) highlightedCells.Add(cell);
		}
		for (var x = 0; x < grid.Width; x++)
		for (var y = 0; y < grid.Height; y++) TintTile(grid.Get(x,y),match.Get(x,y));
		var wanted=highlightedCells.Select(c => (c.X,c.Y)).ToHashSet();
		foreach (var key in outlines.Keys.Where(key => !wanted.Contains(key)).ToArray())
		{
			var item=outlines[key];
			if (!item.Retiring) outlines[key]=(item.Object,0,true);
		}
		foreach (var cell in highlightedCells)
		{
			var key=(cell.X,cell.Y);
			if (outlines.TryGetValue(key,out var existing)) { if (existing.Retiring) outlines[key]=(existing.Object,0,false); TintOutline(existing.Object,cell); continue; }
			var outline=new GameObject(true,$"Highlight {cell.X},{cell.Y}") { Parent=GameObject };
			outline.WorldPosition=grid.CellToWorld(cell.X,cell.Y)+Vector3.Up*(CityGrid.SurfaceHeight+1.5f);
			outline.WorldScale=new Vector3(0.8f,0.8f,1);
			var renderer=outline.Components.Create<ModelRenderer>(); renderer.Model=OutlineModel(); TintOutline(outline,cell);
			outlines[key]=(outline,0,false);
			// A small visual-only pop, without interrupting an existing placement or growth animation.
			var tile=grid.Get(cell.X,cell.Y);
			if (placementInteractions.Count == 0 && !PlayerPreferences.Current.ReduceShake && tile?.Building.IsValid() == true && tile.DropTime <= 0 && !pops.Any(p => p.Tile == tile))
			{
				var rest=Scale(cell); pops.Add((tile,rest*1.04f,rest,0));
			}
		}
	}

	void TintOutline(GameObject outline,BoardCell cell)
	{
		var interaction=placementInteractions.FirstOrDefault(i => i.Cell.X==cell.X && i.Cell.Y==cell.Y);
		var renderer=outline.Components.Get<ModelRenderer>();
		if (renderer != null) renderer.Tint=interaction == null ? Color.White : interaction.Positive && interaction.Negative ? new Color(1f,0.8f,0.45f) : interaction.Negative ? new Color(1f,0.35f,0.35f) : new Color(0.5f,1f,0.55f);
	}
	internal static Model OutlineModel()
	{
		if (outlineModel != null) return outlineModel;
		var v=new VertexBuffer(); v.Init(true);
		// A small printed cardboard frame: an ink outline beneath a mustard inlay.
		void Frame(float half,float width,float z,Color color)
		{
			var length=half*2+width;
			v.AddCube(new Vector3(half,0,z),new Vector3(width,length,0.65f),Rotation.Identity,color);
			v.AddCube(new Vector3(-half,0,z),new Vector3(width,length,0.65f),Rotation.Identity,color);
			v.AddCube(new Vector3(0,half,z),new Vector3(length,width,0.65f),Rotation.Identity,color);
			v.AddCube(new Vector3(0,-half,z),new Vector3(length,width,0.65f),Rotation.Identity,color);
		}
		Frame(CityGrid.CellSize*0.46f,3.2f,0,new Color(0.165f,0.129f,0.094f)); // ink #2a2118
		Frame(CityGrid.CellSize*0.46f,1.5f,0.7f,new Color(0.941f,0.722f,0.227f)); // accent #f0b83a
		var mesh=new Mesh(Material.Load("materials/simple/simple_grey_50.vmat")); mesh.CreateBuffers(v);
		return outlineModel=new ModelBuilder().AddMesh(mesh).Create();
	}
	void ClearOutlines() { foreach (var item in outlines.Values) if (item.Object.IsValid()) item.Object.Destroy(); outlines.Clear(); }
	bool Matches(BoardCell cell) => cell != null && (playerHighlight.HasValue ? cell.Owner == playerHighlight.Value && CardLibrary.Get(cell.CardId).Type != CardType.Road : interactionTargets != null
		? interactionTargets.Any(t => InteractionMatcher.Matches(t,cell,highlightOwner))
		: highlighted != Tag.None && cell.Owner == highlightOwner && CardLibrary.Get(cell.CardId).Tags.HasFlag(highlighted));
	static Color Darken(Color color) => new(color.r*0.78f,color.g*0.78f,color.b*0.78f,color.a);
	void Tint(BoardCell cell) => TintTile(grid.Get(cell.X,cell.Y),cell);
	void TintTile(Tile tile,BoardCell cell)
	{
		if (tile == null) return;
		tile.GroundBaseTint = CardVisualVariation.Ground(cell?.CardId,tile.X,tile.Y,cell != null && Growth.IsMature(cell));
		var dim = placementInteractions.Count == 0 && highlightedCells.Count > 0 && !Matches(cell);
		if (tile.GroundRenderer.IsValid()) tile.GroundRenderer.Tint = dim ? Darken(tile.GroundBaseTint) : tile.GroundBaseTint;
		if (!tile.BuildingRenderer.IsValid()) return;
		var tint = dim ? Darken(tile.BaseTint) : tile.BaseTint;
		tile.BuildingRenderer.Tint = tint;
		CardVisuals.Tint(tile.Building,tint);
	}
	protected override void OnUpdate()
	{
		foreach (var key in outlines.Keys.ToArray())
		{
			var item=outlines[key]; if (!item.Object.IsValid()) { outlines.Remove(key); continue; }
			var duration=item.Retiring ? 0.14f : 0.22f;
			var age=Math.Min(duration,item.Age+Time.Delta); var t=age/duration; var ease=1-MathF.Pow(1-t,3);
			if (item.Retiring && t >= 1) { item.Object.Destroy(); outlines.Remove(key); continue; }
			var scale=item.Retiring ? 1-0.2f*ease : 0.8f+0.2f*ease;
			item.Object.WorldScale=new Vector3(scale,scale,1); outlines[key]=(item.Object,age,item.Retiring);
		}
		for (var i = pops.Count-1; i >= 0; i--)
		{
			var p = pops[i]; if (!p.Tile.Building.IsValid()) { pops.RemoveAt(i); continue; }
			p.Age += Time.Delta; var t = Math.Clamp(p.Age/0.4f,0,1); var s = t-1; var ease = 1+2.70158f*s*s*s+1.70158f*s*s;
			var z = p.From+(p.To-p.From)*ease; var xy = p.To+(p.To-z)*0.4f;
			p.Tile.Building.WorldScale = new Vector3(xy,xy,z)*CityGrid.ModelScale;
			if (t >= 1) { p.Tile.Building.WorldScale = p.To*CityGrid.ModelScale; pops.RemoveAt(i); } else pops[i] = p;
		}
	}

	static Model OwnershipFrame(int seat, Color playerColour)
	{
		if (ownershipModels.TryGetValue(seat,out var cached)) return cached;
		var v=new VertexBuffer(); v.Init(true);
		// A dark keyline and a wide coloured inlay make the owner readable even
		// when a building uses its own textured material. Both sit on the board.
		void Frame(float width,float z,Color colour)
		{
			var half=CityGrid.CellSize*0.45f; var length=half*2+width;
			v.AddCube(new Vector3(half,0,z),new Vector3(width,length,0.35f),Rotation.Identity,colour);
			v.AddCube(new Vector3(-half,0,z),new Vector3(width,length,0.35f),Rotation.Identity,colour);
			v.AddCube(new Vector3(0,half,z),new Vector3(length,width,0.35f),Rotation.Identity,colour);
			v.AddCube(new Vector3(0,-half,z),new Vector3(length,width,0.35f),Rotation.Identity,colour);
		}
		Frame(2.4f,0,new Color(0.165f,0.129f,0.094f));
		Frame(1.3f,0.4f,playerColour);
		var mesh=new Mesh(Material.Load("materials/board/owner_ink.vmat")); mesh.CreateBuffers(v);
		return ownershipModels[seat]=new ModelBuilder().AddMesh(mesh).Create();
	}
	void MarkOwner(BoardCell cell)
	{
		var seat=PlayerColours.Seat(match,cell.Owner);
		if (CardLibrary.Get(cell.CardId).Type == CardType.Road || seat<0) return;
		var frame=new GameObject(true,$"Owner {cell.X},{cell.Y}") { Parent=GameObject };
		frame.WorldPosition=grid.CellToWorld(cell.X,cell.Y)+Vector3.Up*(CityGrid.SurfaceHeight+0.25f);
		frame.WorldRotation=Rotation.Identity;
		frame.WorldScale=Vector3.One;
		var renderer=frame.Components.Create<ModelRenderer>(); renderer.Model=OwnershipFrame(seat,PlayerColours.For(match,cell.Owner));
		ownershipFrames[(cell.X,cell.Y)]=frame;
	}

	void Render( BoardCell cell, bool animate )
	{
		if ( !grid.IsValid() ) return;
		var card = CardLibrary.Get(cell.CardId);
		var type = PlaceholderType(card);
		// Visual choices are derived from cell order, never from the gameplay RNG.
		var piece = new Piece { Type = type, BodyGroup = cell.Order % (type == TileType.House ? 5 : 3), MaterialIndex = cell.Order % 5 };
		if ( !grid.Place(cell.X,cell.Y,piece,animate,legacyRules:false) ) return;
		var tile = grid.Get(cell.X,cell.Y);
		tile.CardId = cell.CardId;
		tile.Owner = cell.Owner;
		tile.ByPlayer = cell.PlacedRound > 0;
		tile.Order = cell.Order;
		tile.Building.Name = $"{(cell.CardId == "sapling" && Growth.IsMature(cell) ? "Forest" : card.Name)} {cell.X},{cell.Y}";
		tile.Building.WorldScale = Scale(cell)*CityGrid.ModelScale;
		tile.BaseTint = Color.White;
		if (type == TileType.Road) grid.RefreshRoad(tile);
		else
		{
			tile.BuildingRenderer.Enabled = false;
			tile.Building.WorldRotation = Rotation.FromYaw(CardVisualVariation.Yaw(match,cell.CardId,cell.X,cell.Y,cell.Order));
			tile.Ground.Enabled = true;
			CardVisuals.Build(tile.Building,cell.CardId,cell.CardId == "sapling" && Growth.IsMature(cell),CardVisualVariation.Seed(cell.CardId,cell.X,cell.Y));
		}
		Tint(cell);
		MarkOwner(cell);
	}

	public static TileType PlaceholderType( CardDef card ) => card.Type switch
	{
		CardType.Road => TileType.Road,
		CardType.Residential => TileType.House,
		CardType.Green or CardType.Growth => TileType.Park,
		_ => TileType.Business
	};
}

