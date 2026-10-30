using System;
using System.Collections.Generic;
using System.Linq;
using CitySim.Cards;

namespace CitySim.Match;

public static class CardDraw
{
	public static int OwnedCount(MatchState state,Guid player,string cardId) => state.Cells().Count(c => c.Owner == player && c.CardId == cardId)+state.Players[player].Hand.Count(id => id == cardId);
	static T Weighted<T>( IReadOnlyList<T> choices, Func<T,int> weight, Random random )
	{
		var roll = random.Next(choices.Sum(weight));
		foreach (var choice in choices) { if (roll < weight(choice)) return choice; roll -= weight(choice); }
		throw new InvalidOperationException("No weighted draw available.");
	}
	public static string Common( Random random ) => Weighted(CardLibrary.All.Where(c => c.Rarity == Rarity.Common && c.Id != "street").ToArray(),c => c.Weight,random).Id;
	public static IReadOnlyList<AuctionLot> Reveal( MatchState state, Random random )
	{
		var eligible = state.Players.Values.Where(p => p.Hand.Count < state.Settings.HandLimit).ToArray();
		var cards = CardLibrary.All.Where(c => eligible.Any(p => c.OwnershipLimit == 0 || OwnedCount(state,p.Id,c.Id) < c.OwnershipLimit)).ToArray();
		var streetPrice = state.Settings.Rarities.First(r => r.Rarity == Rarity.Common).MinimumBid;
		var lots = new List<AuctionLot> { new(0,"street",streetPrice) };
		var remaining = cards.Where(c => c.Id != "street").ToList();
		for (var i = 1; i < 5 && remaining.Count > 0; i++)
		{
			var rarities = state.Settings.Rarities.Where(r => r.UnlockRound <= state.Round && r.RevealWeight > 0 && remaining.Any(c => c.Rarity == r.Rarity)).ToArray();
			if (rarities.Length == 0) break;
			var rarity = Weighted(rarities,r => r.RevealWeight,random);
			var card = Weighted(remaining.Where(c => c.Rarity == rarity.Rarity).ToArray(),c => c.Weight,random);
			remaining.Remove(card);
			lots.Add(new(i,card.Id,rarity.MinimumBid));
		}
		if (lots.Count == 0) throw new InvalidOperationException("No unlocked cards in the market pool.");
		return lots.AsReadOnly();
	}
}
