using CitySim.Cards;
using Sandbox.UI;

namespace CitySim;

public sealed class EffectIcon : Panel
{
	[Parameter] public CardDef Card { get; set; }
	[Parameter] public int Count { get; set; }
	[Parameter] public Action<CardDef> Inspected { get; set; }
	int previousCount;
	float pop = 1;
	Label symbol, badge;
	protected override void OnParametersSet()
	{
		base.OnParametersSet(); AddClass("effect-icon"); AcceptsFocus = true;
		symbol ??= AddChild<Label>("effect-symbol"); badge ??= AddChild<Label>("effect-count");
		symbol.Text = Card.Icon; badge.Text = $"×{Count}";
		Tooltip = $"{Card.Name} ×{Count}\n{Card.Rules}"; TooltipClass = "city-tooltip";
		if (previousCount != Count) { previousCount = Count; pop = 0; }
	}
	protected override void OnMouseOver(MousePanelEvent e) { base.OnMouseOver(e); Inspected?.Invoke(Card); }
	protected override void OnFocus(PanelEvent e) { base.OnFocus(e); Inspected?.Invoke(Card); }
	public override void Tick()
	{
		base.Tick(); if (pop >= 0.3f) return; pop += Time.Delta;
		var s = Math.Clamp(pop/0.3f,0,1)-1; var ease = 1+2.70158f*s*s*s+1.70158f*s*s;
		Style.Set("transform",FormattableString.Invariant($"scale({0.85f+0.15f*ease:0.###})"));
	}
}
