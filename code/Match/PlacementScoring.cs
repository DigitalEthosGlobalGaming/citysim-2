using System;
using System.Collections.Generic;
using System.Linq;
using CitySim.Cards;

namespace CitySim.Match;

public sealed record PlacementEffects( BoardCell Cell, IReadOnlyList<RuleAward> Awards, ParkScoreResult Park );

public static class PlacementScoring
{
	public static PlacementEffects Calculate(MatchState state,BoardCell added)
	{
		var card = CardLibrary.Get(added.CardId); var neighbours = state.Neighbours(added.X,added.Y).ToArray();
		var awards = new List<RuleAward>();
		foreach (var effect in card.Effects.Where(e => e.Hook == EffectHook.Place))
		{
			var vp = effect.Rule switch {
				"adjacent-house-vp" => effect.Amount*neighbours.Count(BoardRules.IsHome),
				"adjacent-home-tag-vp" => effect.Amount*neighbours.Count(c => CardLibrary.Get(c.CardId).Tags.HasFlag(Tag.Home)),
				"adjacent-park-vp" => effect.Amount*neighbours.Count(c => BoardRules.IsPark(c) || c.CardId == "lake"), _ => 0 };
			if (vp != 0) awards.Add(new(added.Owner,vp,0,"Placement adjacency",added));
		}
		ParkScoreResult park = null;
		if (card.ParkSize > 0) { park = ParkScoring.OnTileAdded(state,added); awards.AddRange(park.Awards); }
		if (card.Type != CardType.Road && card.Type != CardType.Event)
		foreach (var source in neighbours.Where(n => n.CardId == "roundabout" && state.Players.ContainsKey(n.Owner)))
		foreach (var effect in CardLibrary.Get(source.CardId).Effects.Where(e => e.Rule == "roundabout-vp"))
			awards.Add(new(source.Owner,effect.Amount,0,"Roundabout",source));
		var owned = state.Cells().Where(c => c.Owner == added.Owner).ToList();
		// Persistent effects may trigger themselves unless the rule explicitly says otherwise.
		foreach (var source in owned.Append(added))
		foreach (var effect in CardLibrary.Get(source.CardId).Effects.Where(e => e.Hook == EffectHook.OwnerPlacedCard))
		{
			if (effect.Rule == "contractor-coins" && (card.Id == "house" || card.Id == "shop") && state.TriggerUses(source.Order,state.Round) < effect.Cap)
				awards.Add(new(added.Owner,0,effect.Amount,"Contractor",source));
			if (effect.Rule == "warehouse-street-coins" && card.Id == "street" && state.TriggerUses(source.Order,state.Round) < effect.Cap)
				awards.Add(new(added.Owner,0,effect.Amount,"Warehouse",source));
			if (effect.Rule == "park-coins" && card.ParkSize > 0) awards.Add(new(added.Owner,0,effect.Amount,"Gardener's Guild",source));
			if (effect.Rule == "first-building-beside-own" && card.Type != CardType.Road &&
				state.BuildingsPlacedThisRound(added.Owner)==0 && neighbours.Any(c => c.Owner == added.Owner))
				awards.Add(new(added.Owner,0,effect.Amount,"Architect's Studio",source));
		}
		return new(added,awards.AsReadOnly(),park);
	}
	public static int Preview(MatchState state,string cardId,int x,int y,Guid? placer = null)
	{
		var owner = placer ?? (state.Players.Count == 1 ? state.Players.Keys.Single() : Guid.Empty);
		var cell = new BoardCell(x,y,cardId,owner,state.NextPlacementOrder,state.Round);
		return Calculate(state,cell).Awards.Where(a => a.Player == owner).Sum(a => a.Vp);
	}
}
