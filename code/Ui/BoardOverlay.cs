using CitySim.Match;
using CitySim.Cards;
using Sandbox.UI;

namespace CitySim;

public sealed class BoardOverlay : MatchPanel
{
	readonly List<(TileClaim Claim,SceneLabel Label)> claims = new();
	readonly Dictionary<(int X,int Y),SceneLabel> highlightLabels = new();
	Panel inspection;
	Label inspectionTitle, inspectionMeta, inspectionRules;
	Label referenceStatus;
	protected override void Subscribe() { if (BoundMatch != null) BoundMatch.Events.ClaimsChanged += Read; if (City?.Player != null) City.Player.PreviewChanged += Inspect; if (City != null) City.UiChanged += Inspect; Read(); Inspect(); }
	protected override void Unsubscribe() { if (BoundMatch != null) BoundMatch.Events.ClaimsChanged -= Read; if (City?.Player != null) City.Player.PreviewChanged -= Inspect; if (City != null) City.UiChanged -= Inspect; foreach (var item in claims) item.Label.Destroy(); claims.Clear(); foreach (var label in highlightLabels.Values) label.Destroy(); highlightLabels.Clear(); }
	void Inspect()
	{
		if (inspection == null)
		{
			inspection=AddChild<Panel>("board-inspect");
			inspectionTitle=inspection.AddChild<Label>("inspect-title");
			inspectionMeta=inspection.AddChild<Label>("inspect-meta");
			inspectionRules=inspection.AddChild<Label>("inspect-rules");
		}
		var hover=City?.Player?.Hovered;
		var cell=hover == null ? null : BoundMatch?.Get(hover.X,hover.Y);
		var visible=cell != null && City.Screens.Top==GameScreen.Match && City.State==GameState.Playing;
		inspection.Style.Display=visible ? DisplayMode.Flex : DisplayMode.None;
		if (!visible) return;
		var card=CardLibrary.Get(cell.CardId);
		var name=cell.CardId=="sapling" && Growth.IsMature(cell) ? "Forest" : card.Name;
		inspectionTitle.Text=$"{card.Icon} {name}";
		var owner=BoundMatch.Players.TryGetValue(cell.Owner,out var player) ? player.Name : "City road";
		inspectionMeta.Text=$"{owner} · {card.Rarity} · {(card.Tags==Tag.None ? "Road" : card.Tags.ToString())}";
		inspectionRules.Text=card.Rules;
		inspectionRules.Tooltip=card.ParkSize > 0 ? CardPresentation.ParkHelp(BoundMatch.Settings) : "Adjacent means a shared edge. Tile ranges include diagonals. Home refers to the Home tag.";
		inspectionRules.TooltipClass="city-tooltip";
	}
	void Read()
	{
		foreach (var item in claims) item.Label.Destroy(); claims.Clear();
		if (BoundMatch == null) return;
		foreach (var claim in BoundMatch.Claims)
		{
			var label = new SceneLabel(City, "Claim", 0.10f); claims.Add((claim,label));
		}
	}
	public override void Tick()
	{
		base.Tick();
		if (referenceStatus == null) referenceStatus = AddChild<Label>("reference-status");
		var showReference = City?.ReferenceTag is { } tag && tag != Tag.None && City.State == GameState.Playing && City.Screens.Top == GameScreen.Match && (City.Match?.Phase != MatchPhase.Bid || Hud.MarketPeeking);
		referenceStatus.Style.Display = showReference ? DisplayMode.Flex : DisplayMode.None;
		if (showReference) referenceStatus.Text = $"{City.HighlightStatus} · Matching cards highlighted";
		var showHighlights = City?.Player != null && City.State == GameState.Playing && City.Screens.Top == GameScreen.Match &&
			(City.Match?.Phase != MatchPhase.Bid || Hud.MarketPeeking);
		var cells = showHighlights ? City.HighlightedCells : Array.Empty<BoardCell>();
		var wanted = cells.Select(c => (c.X,c.Y)).ToHashSet();
		foreach (var key in highlightLabels.Keys.Where(key => !wanted.Contains(key)).ToArray()) { highlightLabels[key].Destroy(); highlightLabels.Remove(key); }
		foreach (var cell in cells)
		{
			var key=(cell.X,cell.Y);
			if (!highlightLabels.TryGetValue(key,out var label)) { label=new SceneLabel(City, "Highlight", 0.14f); highlightLabels[key]=label; }
			label.Set(SceneLabel.AboveTile(City.Grid.Get(cell.X,cell.Y), City.Grid.CellToWorld(cell.X,cell.Y))+Vector3.Up*18, City.HighlightIcon);
		}
		foreach (var item in claims)
		{
			var visible = City.State == GameState.Playing && City.Screens.Top == GameScreen.Match && (City.Match?.Phase != MatchPhase.Bid || Hud.MarketPeeking);
			item.Label.Set(visible ? SceneLabel.AboveTile(City.Grid.Get(item.Claim.X,item.Claim.Y), City.Grid.CellToWorld(item.Claim.X,item.Claim.Y))+Vector3.Up*18 : null, $"🚩 {BoundMatch.Players[item.Claim.Owner].Name}");
		}
	}
}

