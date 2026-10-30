using System;
using System.Collections.Generic;
using System.Linq;
using CitySim.Cards;

namespace CitySim.Match;

public sealed record AuctionLot( int Index, string CardId, int MinimumBid );
public sealed record AuctionWin( Guid Player, int Lot, string CardId, int Price );
public sealed record AuctionAccount( Guid Player, int Reserved, int Paid, int Refunded );
public sealed record AuctionResult( int AuctionId, int Round, int MiniAuction, IReadOnlyList<AuctionWin> Wins,
	IReadOnlyList<AuctionAccount> Accounts, IReadOnlyList<AuctionLot> Discarded );
public sealed record AuctionParticipant( Guid Player, string Name, bool Eligible, bool Ready, bool HasBid = false, int SelectedLot = -1 );
public sealed record OwnLotBid( AuctionLot Lot, int Amount, bool Allowed );

/// <summary>A recipient-specific market projection. Purchases are public; hands remain private.</summary>
public sealed record AuctionView( int Id, int Round, int Number, int Cap, Guid Viewer, int AvailableCoins, int ReservedCoins,
	bool Eligible, bool Ready, bool Resolving, IReadOnlyList<OwnLotBid> Lots, IReadOnlyList<AuctionParticipant> Players );

/// <summary>The host owns the current market selection; checkpoint data records completed purchases.</summary>
internal sealed class AuctionSession
{
	readonly MatchState state;
	readonly Dictionary<Guid,int[]> bids = new();
	readonly HashSet<Guid> ready = new();
	public int Id { get; }
	public int Number { get; }
	public IReadOnlyList<AuctionLot> Lots { get; }
	public bool Resolved { get; private set; }
	public bool AllReady => bids.Keys.All(ready.Contains);
	internal AuctionCheckpoint Capture() => new(Id,Number,Lots.ToArray(),Resolved,bids.Select(p => new BidCheckpoint(p.Key,p.Value.ToArray(),ready.Contains(p.Key))).ToArray());
	internal static AuctionSession Restore(MatchState state,AuctionCheckpoint data)
	{
		var session = new AuctionSession(state,data.Id,data.Number,Array.AsReadOnly(data.Lots.ToArray())) { Resolved = data.Resolved };
		session.bids.Clear(); session.ready.Clear();
		foreach (var p in data.Bids) { session.bids[p.Player] = p.Amounts.ToArray(); if (p.Ready) session.ready.Add(p.Player); }
		return session;
	}
	public AuctionSession( MatchState state, int id, int number, IReadOnlyList<AuctionLot> lots )
	{
		this.state = state; Id = id; Number = number; Lots = lots;
		foreach (var p in state.Players.Values.Where(p => p.Hand.Count < state.Settings.HandLimit))
			bids[p.Id] = new int[lots.Count];
	}
	bool Allowed(Guid owner,AuctionLot lot)
	{
		var card = CardLibrary.Get(lot.CardId);
		return card.OwnershipLimit == 0 || CardDraw.OwnedCount(state,owner,card.Id) < card.OwnershipLimit;
	}
	public AuctionView View( Guid owner )
	{
		var own = bids.GetValueOrDefault(owner);
		var reserved = 0;
		var coins = state.Players.GetValueOrDefault(owner)?.Coins ?? 0;
		return new(Id,state.Round,Number,state.Settings.MiniAuctionCap,owner,coins,reserved,own != null,ready.Contains(owner),Resolved,
			Lots.Select(l => new OwnLotBid(l,own?[l.Index] ?? 0,own != null && Allowed(owner,l) && coins >= l.MinimumBid)).ToList().AsReadOnly(),
			state.Players.Values.Select(p => new AuctionParticipant(p.Id,p.Name,bids.ContainsKey(p.Id),ready.Contains(p.Id),bids.TryGetValue(p.Id,out var amounts) && amounts.Any(amount => amount > 0), bids.TryGetValue(p.Id,out var selections) ? Array.FindIndex(selections, amount => amount > 0) : -1)).ToList().AsReadOnly());
	}
	public bool Buy( Guid owner, int lotIndex )
	{
		if (Resolved || ready.Contains(owner) || !bids.TryGetValue(owner,out var own) || lotIndex < 0 || lotIndex >= Lots.Count) return false;
		var lot = Lots[lotIndex];
		if (!Allowed(owner,lot) || state.Players[owner].Hand.Count >= state.Settings.HandLimit || state.Players[owner].Coins < lot.MinimumBid) return false;
		own[lotIndex] = lot.MinimumBid;
		ready.Add(owner);
		state.ChangeCoins(owner,-lot.MinimumBid);
		state.GiveCard(owner,lot.CardId);
		state.Events.Purchased(new(owner,lot.CardId,lot.MinimumBid,Id,lotIndex));
		state.Events.Bid(owner); state.Events.BidStatus(owner); state.Events.Ready(owner);
		return true;
	}
	public bool Ready( Guid owner )
	{
		if (Resolved || !bids.ContainsKey(owner) || !ready.Add(owner)) return false;
		state.Events.Ready(owner); return true;
	}
	public AuctionResult Resolve()
	{
		if (Resolved) throw new InvalidOperationException("Auction is already resolved.");
		var wins = bids.SelectMany(pair => pair.Value.Select((price,lot) => (pair.Key,lot,price)))
			.Where(choice => choice.price > 0)
			.Select(choice => new AuctionWin(choice.Key,choice.lot,Lots[choice.lot].CardId,choice.price)).ToArray();
		var result = new AuctionResult(Id,state.Round,Number,Array.AsReadOnly(wins),
			Array.AsReadOnly(bids.Keys.Select(player => new AuctionAccount(player,0,wins.FirstOrDefault(w => w.Player == player)?.Price ?? 0,0)).ToArray()),
			Array.AsReadOnly(Lots.Where(lot => wins.All(w => w.Lot != lot.Index)).ToArray()));
		Resolved = true;
		return result;
	}
}
