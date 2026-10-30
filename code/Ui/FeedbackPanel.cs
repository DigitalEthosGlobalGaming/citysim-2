using CitySim.Feel;
using CitySim.Match;
using Sandbox.UI;

namespace CitySim;

/// <summary>Animation updates touch label styles, never the match or the rest of the HUD.</summary>
public sealed class FeedbackPanel : Panel
{
	[Parameter] public CityGame City { get; set; }
	Feedback feedback;
	CityPlayer player;
	CityGame boundCity;
	SceneLabel preview;
	sealed class Floater { public Panel Label; public VisualCue Cue; public float Age; public Vector2 FlightStart; }
	readonly List<Floater> active = new();
	protected override void OnParametersSet()
	{
		base.OnParametersSet();
		if (feedback == City?.Feedback) return;
		if (feedback != null) { SceneLabel.ClearTransient(boundCity); preview?.Destroy(); preview = null; feedback.Cue -= Show; feedback.TallySkipped -= ClearPayout; feedback.Cleared -= ClearAll; }
		if (player != null) player.PreviewChanged -= Preview;
		if (boundCity != null) boundCity.UiChanged -= Preview;
		boundCity = City;
		feedback = City?.Feedback;
		player = City?.Player;
		if (feedback != null) { feedback.Cue += Show; feedback.TallySkipped += ClearPayout; feedback.Cleared += ClearAll; }
		if (player != null) player.PreviewChanged += Preview;
		if (City != null) City.UiChanged += Preview;
		Preview();
	}
	void Preview()
	{
		if (City == null || !City.IsValid()) return;
		preview ??= new SceneLabel(City, "Placement preview", 0.12f);
		// The ghost tint shows validity; only a nonzero VP change needs a label.
		Vector3? position = City.BoardInput && player?.Hovered is { } tile && City.SelectedCard != null && player.HoveredValid && player.HoveredGain != 0
			? SceneLabel.AboveVisual(player.PlacementGhost, SceneLabel.AboveTile(tile, tile.Center))+Vector3.Up*18 : null;
		preview.Set(position, player?.HoveredGain != 0 ? $"{player.HoveredGain:+#;-#} VP" : "");
	}
	void Show( VisualCue cue )
	{
		if (cue.Confetti)
		{
			foreach (var old in active) Remove(old); active.Clear();
			preview?.Set(null, "");
			if (City.Feedback.ReduceFlashing) return;
			for (var i = 0; i < 20; i++)
			{
				var part = AddChild<Label>("confetti"); part.Text = new[] { "✦", "●", "■" }[i%3];
				part.Style.Set("color",i%2 == 0 ? "#ffda78" : "#8ed7ff");
				active.Add(new() { Label = part, Cue = cue, FlightStart = new Vector2(0.05f+i*0.047f,-0.15f-(i%4)*0.05f) });
			}
			return;
		}
		if (cue.Region != null) return;
		if (!cue.Banner && cue.CardId == null) { SceneLabel.Spawn(City, cue); return; }
		if (cue.Banner && !cue.PayoutReceipt && active.Any(f => f.Cue.Banner && !f.Cue.PayoutReceipt && f.Cue.Tier > cue.Tier)) return;
		if (cue.Banner)
			foreach (var old in active.Where(f => f.Cue.Banner && f.Cue.PayoutReceipt == cue.PayoutReceipt).ToArray()) { Remove(old); active.Remove(old); }
		if (active.Count >= 30) { Remove(active[0]); active.RemoveAt(0); }
		Panel label;
		if (cue.PayoutReceipt) label = AddChild<Panel>();
		else { var textLabel = AddChild<Label>(); textLabel.Text = cue.Text; label = textLabel; }
		label.AddClass(cue.CardId != null ? "auction-flight" : cue.Banner ? "phase-toast" : "floater");
		if (cue.PlacementVp) label.AddClass("placement-vp");
		if (cue.PayoutReceipt)
		{
			label.AddClass("payout-receipt");
			var lines = cue.Text.Split('\n');
			label.AddChild<Label>("receipt-title").Text = lines[0];
			foreach (var line in lines.Skip(1))
			{
				var columns = line.Split('\t');
				var row = label.AddChild<Panel>("receipt-row");
				row.AddChild<Label>("receipt-source").Text = columns[0];
				row.AddChild<Label>("receipt-amount").Text = columns.Length > 1 ? columns[1] : "";
			}
		}
		var source = City.Scene.GetAllComponents<Hud>().FirstOrDefault()?.Panel.Descendants.OfType<AuctionLotPanel>().FirstOrDefault(p => p.LotIndex == cue.Lot);
		var floater = new Floater { Label = label, Cue = cue, FlightStart = source != null ? source.Box.Rect.Center / Screen.Size : new Vector2(0.5f,0.45f) };
		active.Add(floater);
	}
	void Remove(Floater item) { item.Label.Delete(); }
	void ClearPayout()
	{
		SceneLabel.ClearTransient(City);
		foreach (var item in active.Where(f=>f.Cue.PayoutCoin || f.Cue.PayoutReceipt).ToArray()) { Remove(item); active.Remove(item); }
	}
	void ClearAll()
	{
		foreach (var item in active) Remove(item);
		active.Clear(); preview?.Set(null, "");
		SceneLabel.ClearTransient(City);
	}
	public override void Tick()
	{
		base.Tick();
		foreach (var item in active.ToArray())
		{
			item.Age += Time.Delta;
			var life = item.Cue.PayoutReceipt ? 6.5f : item.Cue.PayoutCoin ? 0.85f : item.Cue.Confetti ? 3f : item.Cue.Region != null ? 0.7f : item.Cue.Banner ? 2.2f : 1.4f;
			if (item.Age >= life) { Remove(item); active.Remove(item); continue; }
			item.Label.Style.Opacity = Math.Clamp((life-item.Age)*3,0,1);
			if (item.Cue.Confetti)
			{
				var t = item.Age/life;
				item.Label.Style.Left = Length.Percent((item.FlightStart.x+MathF.Sin(t*5+item.FlightStart.x*12)*0.035f)*100);
				item.Label.Style.Top = Length.Percent((item.FlightStart.y+t*t*1.4f)*100); continue;
			}
			if (item.Cue.CardId != null)
			{
				var t = Math.Clamp(item.Age/1.1f,0,1);
				var ease = 1-MathF.Pow(1-t,3);
				var flightPoint = item.FlightStart+(new Vector2(0.5f,0.87f)-item.FlightStart)*ease;
				item.Label.Style.Left = Length.Percent(flightPoint.x*100);
				item.Label.Style.Top = Length.Percent(flightPoint.y*100-MathF.Sin(t*MathF.PI)*12);
				continue;
			}
			if (item.Cue.Banner)
			{
				if (item.Cue.PayoutReceipt)
				{
					item.Label.Style.Left=Length.Percent(35); item.Label.Style.Right=Length.Percent(35);
					item.Label.Style.Top=Length.Percent(25);
					continue;
				}
				var inspecting = City.Screens.Top == GameScreen.Goals;
				item.Label.Style.Left = Length.Percent(inspecting ? 45 : 40); item.Label.Style.Right = Length.Percent(inspecting ? 15 : 40);
				item.Label.Style.Top = Length.Percent(16+3*MathF.Pow(1-Math.Clamp(item.Age/0.3f,0,1),3));
			}

		}
	}
	public override void OnDeleted() { if (feedback != null) { feedback.Cue -= Show; feedback.TallySkipped -= ClearPayout; feedback.Cleared -= ClearAll; } if (player != null) player.PreviewChanged -= Preview; if (boundCity != null) boundCity.UiChanged -= Preview; preview?.Destroy(); SceneLabel.ClearTransient(boundCity); base.OnDeleted(); }
}

