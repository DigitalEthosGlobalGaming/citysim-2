using CitySim.Cards;
using CitySim.Match;

namespace CitySim;

/// <summary>Board picking and keyboard cursor. Placement always goes through the same model validator.</summary>
public sealed class CityPlayer : Component
{
	public Tile Hovered { get; private set; }
	public bool HoveredValid { get; private set; }
	public int HoveredGain { get; private set; }
	public string HoveredReason { get; private set; }
	public Vector2? DebugMouse { get; set; }
	public event Action PreviewChanged;
	public IReadOnlyList<PlacementInteraction> Interactions { get; private set; } = Array.Empty<PlacementInteraction>();
	string interactionKey;
	MatchState interactionMatch;
	GameObject footprint;
	static Model footprintModel;
	GameObject ghost;
	public GameObject PlacementGhost => ghost;
	ModelRenderer ghostRenderer;
	string ghostModel;
	float denyTime;
	bool keyboardCursor;
	int cursorX = 5, cursorY = 5;
	Vector2 previousMouse;
	CameraComponent Camera => Components.Get<CameraComponent>();
	public CityCamera View => Components.GetOrCreate<CityCamera>();
	public void FocusOn( CityGrid grid ) { View.FocusOn(grid); cursorX = grid.Width/2; cursorY = grid.Height/2; }
	public void Deny() => denyTime = 0.2f;

	protected override void OnUpdate()
	{
		var game = CityGame.Instance;
		if (game == null || !game.Grid.IsValid() || !game.Grid.IsBuilt) return;
		var inspectingMarket = Hud.MarketPeeking && game.Match?.Phase == MatchPhase.Bid && game.Screens.Top == GameScreen.Match;
		View.Step((game.BoardInput || inspectingMarket || game.EndInspect) && !Hud.ControlsMode);
		denyTime = Math.Max(0,denyTime-Time.Delta);
		var old = (Hovered,HoveredValid,HoveredGain,HoveredReason);
		if ((game.BoardInput || inspectingMarket) && !Hud.ControlsMode)
		{
			HandleCursor(game);
			UpdateHover(game);
			if (game.BoardInput) HandleActions(game);
		}
		else { Hovered = null; HoveredValid = false; HoveredGain = 0; HoveredReason = ""; }
		UpdateInteractions(game);
		UpdateGhost(game);
		if (old != (Hovered,HoveredValid,HoveredGain,HoveredReason)) PreviewChanged?.Invoke();
	}

	void HandleCursor( CityGame game )
	{
		var mouse = DebugMouse ?? Mouse.Position;
		if ((mouse-previousMouse).Length > 2) keyboardCursor = false;
		previousMouse = mouse;
		var x = (Input.Pressed("CursorRight") ? 1 : 0)-(Input.Pressed("CursorLeft") ? 1 : 0);
		var y = (Input.Pressed("CursorDown") ? 1 : 0)-(Input.Pressed("CursorUp") ? 1 : 0);
		if (x != 0 || y != 0) { FocusBoard(game); keyboardCursor = true; cursorX = Math.Clamp(cursorX+x,0,game.Grid.Width-1); cursorY = Math.Clamp(cursorY+y,0,game.Grid.Height-1); }
	}
	void UpdateHover( CityGame game )
	{
		Tile tile = keyboardCursor ? game.Grid.Get(cursorX,cursorY) : null;
		if (!keyboardCursor && Camera != null && !Hud.PointerOverUi)
		{
			var ray = Camera.ScreenPixelToRay(DebugMouse ?? Mouse.Position);
			if (ray.Forward.z < -0.001f)
			{
				var t = (CityGrid.SurfaceHeight-ray.Position.z)/ray.Forward.z;
				if (t > 0) tile = game.Grid.WorldToTile(ray.Position+ray.Forward*t);
			}
		}
		Hovered = tile; HoveredValid = false; HoveredGain = 0;
		HoveredReason = "Select a card";
		if (!game.BoardInput || tile == null || game.SelectedCardId == null) return;
		if (game.RenovationArmed)
		{
			HoveredReason = game.RenovationReplacementSlot < 0 ? "Choose a replacement building from your hand" : game.Match.RenovationError(CityGame.Me,game.RenovationEventSlot,game.RenovationReplacementSlot,tile.X,tile.Y);
			HoveredValid = HoveredReason == null;
			if (HoveredValid) HoveredGain = game.Match.PreviewRenovation(CityGame.Me,game.RenovationEventSlot,game.RenovationReplacementSlot,tile.X,tile.Y) ?? 0;
			return;
		}
		HoveredReason = game.Match.CardPlayError(CityGame.Me,game.SelectedCardId,tile.X,tile.Y);
		HoveredValid = HoveredReason == null;
		if (HoveredValid) HoveredGain = PlacementScoring.Preview(game.Match,game.SelectedCardId,tile.X,tile.Y,CityGame.Me);
	}
	void UpdateInteractions(CityGame game)
	{
		var card = game.RenovationArmed ? game.RenovationReplacement?.Id : game.SelectedCardId;
		var key = game.BoardInput && game.Match != null && Hovered != null && card != null && card != "land-claim" && !game.Held.IsEmpty
			? $"{card}:{Hovered.X}:{Hovered.Y}:{game.RenovationArmed}:" + string.Join(",",game.Match.Cells().Select(c => $"{c.Order}:{c.Age}")) + ":" + game.Match.Round + ":" + string.Join(",",game.Match.Players.Values.Select(p => $"{p.Coins}:{p.Vp}")) : "";
		if (key == interactionKey && interactionMatch == game.Match) return;
		interactionMatch = game.Match; interactionKey = key;
		Interactions = key.Length == 0 ? Array.Empty<PlacementInteraction>() : game.Match.PreviewInteractions(CityGame.Me,card,Hovered.X,Hovered.Y,game.RenovationArmed);
		game.PreviewPlacementInteractions(Interactions);
		PreviewChanged?.Invoke();
	}

	void HandleActions( CityGame game )
	{
		for (var i = 0; i < 5; i++) if (Input.Pressed($"Slot{i+1}")) { FocusBoard(game); game.SelectSlot(i); }
		if (Input.Pressed("HandPrevious")) game.SelectSlot(Math.Max(0,game.SelectedSlot-1));
		if (Input.Pressed("HandNext")) game.SelectSlot(Math.Min((game.LocalPlayer?.Hand.Count ?? 1)-1,game.SelectedSlot+1));
		if (Input.Pressed("Attack2") || Input.Pressed("CancelCard")) game.Deselect();
		if (Input.Pressed("DiscardCard")) game.DiscardSelected();
		if (Hud.PointerOverUi && !keyboardCursor) return;
		if (Input.Pressed("Attack1") || Input.Pressed("ConfirmCard") && !Hud.UiControlFocused)
		{
			if (game.SelectedImmediateEvent && !game.RenovationArmed) game.PlaySelectedEvent();
			else if (Hovered != null && game.SelectedCardId != null) game.RequestPlace(Hovered.X,Hovered.Y);
		}
	}
	void FocusBoard(CityGame game) => game.Scene.GetAllComponents<Hud>().FirstOrDefault()?.FocusBoard();
	void UpdateGhost( CityGame game )
	{
		if (!game.BoardInput || Hovered == null || game.Held.IsEmpty)
		{
			if (ghost.IsValid()) ghost.Enabled = false;
			if (footprint.IsValid()) footprint.Enabled = false;
			return;
		}
		var occupied = game.Match.Get(Hovered.X,Hovered.Y) != null;
		var showFootprint = HoveredValid && !occupied && game.SelectedCardId != "land-claim";
		if (showFootprint)
		{
			if (!footprint.IsValid())
			{
				footprint = new GameObject(true,"Placement footprint") { Parent = GameObject, NetworkMode = NetworkMode.Never };
				if (footprintModel == null)
				{
					var v = new VertexBuffer(); v.Init(true);
					var half = CityGrid.CellSize * 0.43f; var length = half * 2 + 1.5f;
					var ink = new Color(0.25f,0.55f,0.25f);
					v.AddCube(new Vector3(half,0,0),new Vector3(1.5f,length,0.35f),Rotation.Identity,ink);
					v.AddCube(new Vector3(-half,0,0),new Vector3(1.5f,length,0.35f),Rotation.Identity,ink);
					v.AddCube(new Vector3(0,half,0),new Vector3(length,1.5f,0.35f),Rotation.Identity,ink);
					v.AddCube(new Vector3(0,-half,0),new Vector3(length,1.5f,0.35f),Rotation.Identity,ink);
					var mesh = new Mesh(Material.Load("materials/board/owner_ink.vmat")); mesh.CreateBuffers(v);
					footprintModel = new ModelBuilder().AddMesh(mesh).Create();
				}
				footprint.Components.Create<ModelRenderer>().Model = footprintModel;
			}
			footprint.WorldRotation = Rotation.Identity;
			footprint.WorldPosition = Hovered.Center + Vector3.Up * (CityGrid.SurfaceHeight + 0.3f);
		}
		if (footprint.IsValid()) footprint.Enabled = showFootprint;
		if (occupied && !game.RenovationArmed)
		{
			if (ghost.IsValid()) ghost.Enabled = false;
			return;
		}
		if (!ghost.IsValid())
		{
			ghost = new GameObject(true,"Placement ghost") { Parent = GameObject };
			ghost.WorldScale = CityGrid.ModelScale;
			ghostRenderer = ghost.Components.Create<ModelRenderer>();
		}
		var type = game.Held.Type;
		var id = game.SelectedCardId;
		var road = game.Grid.RoadShape(Hovered.X,Hovered.Y);
		var model = id == "land-claim" ? TileRules.Ghost : type == TileType.Road ? road.model : null;
		if (id == "avenue" && model != null && model.EndsWith("road-crossroad.vmdl")) model = "models/kenney/roads/road-crossroad-line.vmdl";
		var key = id + ":" + model;
		if (ghostModel != key)
		{
			ghostModel = key;
			ghostRenderer.Enabled = id == "land-claim";
			if (id == "land-claim") ghostRenderer.Model = Model.Load(model);
			if (id == "land-claim") CardVisuals.Clear(ghost);
			else if (type == TileType.Road) CardVisuals.BuildRoad(ghost,model,id == "roundabout");
			else CardVisuals.Build(ghost,id);
		}
		var dir = game.Grid.RoadNeighbourDirection(Hovered);
		var yaw = type == TileType.Road ? road.yaw : dir >= 0 ? CityGrid.YawForDirection(dir) : 0;
		ghost.WorldRotation = Rotation.FromYaw(yaw+(denyTime > 0 ? MathF.Sin(denyTime*100)*denyTime*40 : 0));
		// Road meshes normally replace the ground tile. Lift the preview above the
		// still-visible ground so it cannot disappear underneath it.
		ghost.WorldPosition = Hovered.Center+Vector3.Up*(type == TileType.Road && id != "land-claim" ? CityGrid.SurfaceHeight+0.8f : Hovered.Type == TileType.Empty ? 0 : 4);
		ghost.Enabled = true;
		var tint = HoveredValid ? new Color(0.7f,1,0.7f) : new Color(1,0.4f,0.4f);
		if (id == "avenue") tint *= new Color(0.8f,0.85f,1f);
		ghostRenderer.Tint = tint; CardVisuals.Tint(ghost,tint);

	}
	public Vector2? ScreenPoint( Vector3 world )
	{
		if (Camera == null) return null;
		if ((world-Camera.WorldPosition).Dot(Camera.WorldRotation.Forward) <= 0) return null;
		var rect = Camera.BBoxToScreenPixels(new BBox(world-Vector3.One,world+Vector3.One),out _);
		return rect.Center / Screen.Size;
	}
	protected override void OnDestroy() { if (ghost.IsValid()) ghost.Destroy(); if (footprint.IsValid()) footprint.Destroy(); }
}

