using System;
using System.Collections.Generic;
using System.Linq;
using CitySim.Cards;

namespace CitySim.Match;

public sealed record ScoreLine(Guid Player,string Reason,int Vp,string Detail = "",BoardCell Source = null,bool SecretContract = false);
public sealed record PlayerScore(Guid Player,string Name,int EarnedVp,int BonusVp,int TotalVp,int Coins,IReadOnlyList<ScoreLine> Lines);
public sealed record EndScore(IReadOnlyList<PlayerScore> Players,IReadOnlyList<Guid> Winners);

public static class FinalScoring
{
	public static EndScore Calculate(MatchState state)
	{
		if (state.FinalScore != null) return state.FinalScore;
		var cells = state.Cells().OrderBy(c => c.Order).ToArray(); var lines = new List<ScoreLine>();
		foreach (var player in state.Players.Values)
		{
			var goalVp = state.Goals.History.SelectMany(c => c.Awards).Where(a => a.Player == player.Id).Sum(a => a.Vp);
			lines.Add(new(player.Id,"Earned during match",player.Vp-goalVp,"Placement, parks and other match awards"));
		}
		foreach (var claim in state.Goals.History) foreach (var award in claim.Awards) lines.Add(new(award.Player,"Public goal · "+GoalLibrary.Get(claim.GoalId).Name,award.Vp,$"Round {claim.Round} · {(claim.Awards.Count > 1 ? "shared claim" : "sole claim")}"));
		lines.AddRange(CardEffects(state));
		void Competition(string name,int pool,Func<Guid,int> measure)
		{
			var scores = state.Players.Keys.ToDictionary(id => id,measure);
			var maximum = scores.Count > 0 ? scores.Values.Max() : 0;
			var tied = scores.Count(p => p.Value == maximum);
			foreach (var p in scores)
				lines.Add(new(p.Key,name,maximum > 0 && p.Value == maximum ? pool/tied : 0,$"Your measure {p.Value}; leader {maximum}. " + (maximum <= 0 ? "No qualifying entry." : tied > 1 ? $"{tied} tied; {pool} VP split, rounded down." : $"Winner receives {pool} VP.")));
		}
		var parks = ParkScoring.Regions(state);
		Competition("Longest Road",state.Settings.RoadBonusVp,id => RoadScoring.Longest(state,id));
		Competition("Biggest Park Owner",state.Settings.ParkBonusVp,id => parks.Select(region => region.Count(c => c.Owner == id)).DefaultIfEmpty(0).Max());
		Competition("Full House",state.Settings.HousesBonusVp,id => cells.Count(c => c.Owner == id && BoardRules.IsHome(c)));
		foreach (var reveal in state.ContractReveals) lines.Add(new(reveal.Player,"Secret contract · "+Contracts.Get(reveal.ContractId).Name,reveal.Vp,reveal.Detail+(reveal.Complete ? " · fulfilled" : " · not fulfilled"),SecretContract:true));
		foreach (var p in state.Players.Values) lines.Add(new(p.Id,"Spare coins",p.Coins/state.Settings.CoinsPerEndVp,$"{p.Coins} coins / {state.Settings.CoinsPerEndVp}, rounded down"));
		var players = state.Players.Values.Select(p =>
		{
			var ownLines = lines.Where(l => l.Player == p.Id).ToList().AsReadOnly(); var total = ownLines.Sum(l => l.Vp);
			return new PlayerScore(p.Id,p.Name,p.Vp,total-p.Vp,total,p.Coins,ownLines);
		}).OrderByDescending(p => p.TotalVp).ThenByDescending(p => p.Coins).ThenBy(p => p.Player).ToList();
		var first = players.FirstOrDefault();
		return new(players.AsReadOnly(),players.Where(p => p.TotalVp == first.TotalVp && p.Coins == first.Coins).Select(p => p.Player).ToList().AsReadOnly());
	}
	/// <summary>Pure per-card end-game awards, in placement order, without competition or secret scoring.</summary>
	public static IReadOnlyList<ScoreLine> CardEffects(MatchState state)
	{
		var cells = state.Cells().OrderBy(c => c.Order).ToArray();
		var byOwner = cells.ToLookup(c => c.Owner);
		var lines = new List<ScoreLine>();
		var airportLoss = new Dictionary<Guid,int>();
		foreach (var cell in cells)
		foreach (var effect in CardLibrary.Get(cell.CardId).Effects.Where(e => e.Hook == EffectHook.EndGame))
		{
			var card = CardLibrary.Get(cell.CardId); var own = byOwner[cell.Owner];
			if (effect.Rule == "vp") lines.Add(new(cell.Owner,card.Name,effect.Amount,"End-game card",cell));
			if (effect.Rule == "stadium-house-vp") lines.Add(new(cell.Owner,card.Name,state.Neighbours(cell.X,cell.Y).Count(BoardRules.IsHome)*effect.Amount,"Adjacent Home buildings, any owner",cell));
			if (effect.Rule == "nearby-building-vp") lines.Add(new(cell.Owner,card.Name,Math.Min(effect.Cap,cells.Count(c => c.Order != cell.Order && CardLibrary.Get(c.CardId).Type is not (CardType.Road or CardType.Event) && BoardRules.Distance(cell,c) <= effect.Range)*effect.Amount),$"Buildings within {effect.Range}, any owner; cap {effect.Cap}",cell));
			if (effect.Rule == "airport-noise")
				foreach (var victim in cells.Where(c => BoardRules.IsHome(c) && BoardRules.Distance(cell,c) <= effect.Range))
				{
					var loss = Math.Min(effect.Amount,Math.Max(0,effect.Cap-airportLoss.GetValueOrDefault(victim.Owner)));
					if (loss == 0) continue;
					airportLoss[victim.Owner] = airportLoss.GetValueOrDefault(victim.Owner)+loss;
					lines.Add(new(victim.Owner,"Airport noise",-loss,$"{CardLibrary.Get(victim.CardId).Name} at {victim.X},{victim.Y}; combined Airport penalty cap {effect.Cap}",cell));
				}
			if (effect.Rule == "tag-majorities-vp")
			{
				var tags = new[] { Tag.Home,Tag.Trade,Tag.Nature,Tag.Industry,Tag.Culture,Tag.Civic };
				var qualified = tags.Where(tag => own.Count(c => CardLibrary.Get(c.CardId).Type is not (CardType.Road or CardType.Event) && CardLibrary.Get(c.CardId).Tags.HasFlag(tag)) >= effect.Divisor).ToArray();
				lines.Add(new(cell.Owner,card.Name,Math.Min(effect.Cap,qualified.Length*effect.Amount),$"{qualified.Length} tags with at least {effect.Divisor} buildings: {string.Join(", ",qualified)}; cap {effect.Cap}",cell));
			}
			if (effect.Rule == "other-culture-vp")
			{
				var count = own.Count(c => c.Order != cell.Order && CardLibrary.Get(c.CardId).Tags.HasFlag(Tag.Culture));
				lines.Add(new(cell.Owner,card.Name,Math.Min(effect.Cap,count*effect.Amount),$"{count} other Culture buildings; cap {effect.Cap}",cell));
			}
			if (effect.Rule == "home-thirds-vp")
			{
				var count = own.Count(c => CardLibrary.Get(c.CardId).Tags.HasFlag(Tag.Home));
				lines.Add(new(cell.Owner,card.Name,Math.Min(effect.Cap,count/effect.Divisor*effect.Amount),$"{count} Home buildings; 1 VP per {effect.Divisor}, cap {effect.Cap}",cell));
			}
			if (effect.Rule == "eco-council-vp")
			{
				var nature = own.Count(c => CardLibrary.Get(c.CardId).Tags.HasFlag(Tag.Nature));
				var industry = own.Count(c => CardLibrary.Get(c.CardId).Tags.HasFlag(Tag.Industry));
				var vp = Math.Max(0,Math.Min(effect.Cap,nature/effect.Divisor*effect.Amount)-effect.Range*industry);
				lines.Add(new(cell.Owner,card.Name,vp,$"{nature} Nature buildings, {industry} Industry buildings; cap {effect.Cap}, minus {effect.Range} per Industry",cell));
			}
			if (effect.Rule == "beside-park-vp") lines.Add(new(cell.Owner,card.Name,state.Neighbours(cell.X,cell.Y).Any(c => BoardRules.IsPark(c) || c.CardId == "lake") ? effect.Amount : 0,"End bonus if adjacent to a park",cell));
			if (effect.Rule == "pollution")
				foreach (var victim in state.Neighbours(cell.X,cell.Y).Where(c => BoardRules.IsHome(c) || c.CardId == "cafe"))
					lines.Add(new(victim.Owner,"Factory pollution",-effect.Amount,$"{CardLibrary.Get(victim.CardId).Name} at {victim.X},{victim.Y} beside Factory at {cell.X},{cell.Y}",cell));
		}
		return lines.AsReadOnly();
	}

	internal static EndScore Apply(MatchState state)
	{
		if (state.FinalScore != null) return state.FinalScore;
		var result = Calculate(state); state.FinalScore = result;
		foreach (var player in result.Players) state.ChangeVp(player.Player,player.BonusVp);
		state.Events.Scored(result); return result;
	}
}
