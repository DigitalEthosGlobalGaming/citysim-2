using System;
using System.Collections.Generic;
using System.Linq;
using CitySim.Cards;

namespace CitySim.Match;

public sealed record PayoutEntry( Guid Player, int Coins, int Vp, string Reason, BoardCell Source = null, BoardCell Partner = null );
public sealed record PayoutResult( int Round, IReadOnlyList<PayoutEntry> Entries )
{
	public int CoinsFor( Guid player ) => Entries.Where(e => e.Player == player).Sum(e => e.Coins);
}

public static class Payout
{
	public static PayoutResult Calculate( MatchState state )
	{
		// Preserve row-major order for each owner's entries; their stable order is visible in the tally.
		var cells = state.Cells().ToArray();
		var byOwner = cells.ToLookup(c => c.Owner);
		var entries = new List<PayoutEntry>();
		var rush = CardLibrary.Get("rush-hour").Effects.First(e => e.Rule == "rush-hour");
		foreach (var player in state.Players.Values.Where(p => state.RushHourActive(p.Id)))
		{
			var received = byOwner[player.Id].Count(c => c.CardId == "shop");
			var bonus = Math.Min(rush.Cap,received * rush.Amount);
			if (bonus > 0) entries.Add(new(player.Id,bonus,0,"Rush Hour"));
		}
		foreach ( var cottage in cells.Where(c => c.CardId == "cottage") )
			if ( state.Neighbours(cottage.X,cottage.Y).Any(c => BoardRules.IsPark(c) || c.CardId == "lake") )
				entries.Add(new(cottage.Owner,CardLibrary.Get("cottage").Effects.First(e => e.Rule == "beside-park-coins").Amount,0,"Beside park",cottage));
		foreach (var cell in cells.OrderBy(c => c.Order))
		foreach (var effect in CardLibrary.Get(cell.CardId).Effects.Where(e => e.Hook == EffectHook.Payout))
		{
			var own = byOwner[cell.Owner];
			var amount = effect.Rule switch
			{
				"coins" => effect.Amount,
				"avenue-buildings" => state.Neighbours(cell.X,cell.Y).Count(c => CardLibrary.Get(c.CardId).Type is not (CardType.Road or CardType.Event)) >= effect.Cap ? effect.Amount : 0,
				"nearby-shops" => Math.Min(effect.Cap,own.Count(c => c.CardId == "shop" && BoardRules.Distance(cell,c) <= effect.Range)*effect.Amount),
				"delayed-coins" => cell.Age >= effect.Cap ? effect.Amount : 0,
				"ramping-coins" => Math.Min(effect.Cap,(cell.Age+1)*effect.Amount),
				"civic-threshold" => own.Count(c => CardLibrary.Get(c.CardId).Tags.HasFlag(Tag.Civic)) >= effect.Cap ? effect.Amount : 0,
				"shop-condition" => cells.Any(c => c.CardId == "shop" && BoardRules.Distance(cell,c) <= effect.Range) ? effect.Amount : 0,
                "nearby-any-homes" => Math.Min(effect.Cap,cells.Count(c => BoardRules.IsHome(c) && BoardRules.Distance(cell,c) <= effect.Range)*effect.Amount),
                "adjacent-farm-condition" => state.Neighbours(cell.X,cell.Y).Any(BoardRules.IsFarm) ? effect.Amount : 0,
                "cafe-neighbours" => Math.Min(effect.Cap,state.Neighbours(cell.X,cell.Y).Count(c => BoardRules.IsHome(c) || c.CardId == "shop")*effect.Amount),
                "owned-farms" => Math.Min(effect.Cap,own.Count(BoardRules.IsFarm)*effect.Amount),
                "owned-shops-markets" => Math.Min(effect.Cap,own.Count(c => c.CardId is "shop" or "market")*effect.Amount),
				"culture-count" => Math.Min(effect.Cap,own.Count(c => CardLibrary.Get(c.CardId).Tags.HasFlag(Tag.Culture))*effect.Amount),
				"house-groups" => own.Count(BoardRules.IsHome)/effect.Divisor*effect.Amount,
				"road-groups" => Math.Min(effect.Cap,own.Count(c => CardLibrary.Get(c.CardId).Type == CardType.Road)/effect.Divisor*effect.Amount),
				"nearby-homes" => Math.Min(effect.Cap,own.Count(c => BoardRules.IsHome(c) && BoardRules.Distance(cell,c) <= effect.Range)*effect.Amount),
				"industry-count" => Math.Min(effect.Cap,own.Count(c => CardLibrary.Get(c.CardId).Tags.HasFlag(Tag.Industry))*effect.Amount),
				"trade-pairs" => Math.Min(effect.Cap,own.Count(c => CardLibrary.Get(c.CardId).Tags.HasFlag(Tag.Trade))/effect.Divisor*effect.Amount),
				_ => 0
			};
			if (amount > 0) entries.Add(new(cell.Owner,amount,0,CardLibrary.Get(cell.CardId).Name,cell));
			if (effect.Rule == "lake-houses")
				foreach (var home in state.Neighbours(cell.X,cell.Y).Where(BoardRules.IsHome))
					entries.Add(new(home.Owner,effect.Amount,0,"Beside Lake",home,cell));
			if (effect.Rule == "stadium-neighbours")
				foreach (var neighbour in state.Neighbours(cell.X,cell.Y))
				{
					if (BoardRules.IsHome(neighbour)) entries.Add(new(neighbour.Owner,effect.Amount,0,"Stadium · adjacent home",neighbour,cell));
					if (neighbour.CardId == "cafe") entries.Add(new(neighbour.Owner,-effect.Amount,0,"Stadium · Cafe noise",neighbour,cell));
				}
		}
		// A source-ordered result is also the view's animation script. No car arrival changes it.
		entries = entries.OrderBy(e => e.Source?.Order ?? int.MaxValue).ToList();
		var lastVp = state.Players.Count == 0 ? 0 : state.Players.Values.Min(p => p.Vp);
		foreach ( var player in state.Players.Values )
		{
			entries.Add(new(player.Id,state.Settings.BaseIncome,0,"Base income"));
			if ( state.Round > 1 && player.Vp == lastVp ) entries.Add(new(player.Id,state.Settings.CatchUpCoins,0,"Catch-up"));
		}
		// Noise can consume this payout's income and saved coins, but cannot create debt.
		// Clip explained deductions before settlement so their sum always matches the bank change.
		foreach (var player in state.Players.Values)
		{
			long available = player.Coins + entries.Where(e => e.Player == player.Id && e.Coins > 0).Sum(e => (long)e.Coins);
			for (var i=0;i<entries.Count;i++)
			{
				var entry=entries[i]; if (entry.Player != player.Id || entry.Coins >= 0) continue;
				var loss=(int)Math.Min(available,-(long)entry.Coins); available-=loss;
				entries[i]=entry with { Coins=-loss };
			}
		}
		return new(state.Round,entries.AsReadOnly());
	}

	internal static bool Apply( MatchState state, PayoutResult result )
	{
		if (state.Phase != MatchPhase.Payout || result.Round != state.Round || result.Round <= state.LastPaidRound) return false;
		state.LastPaidRound = result.Round;
		foreach (var group in result.Entries.GroupBy(e => e.Player)) state.ChangeCoins(group.Key,group.Sum(e => e.Coins));
		foreach ( var entry in result.Entries )
		{
			if (entry.Vp != 0) state.ChangeVp(entry.Player,entry.Vp);
		}
		state.Events.Payout(result);
		Growth.Advance(state);
		return true;
	}
}
