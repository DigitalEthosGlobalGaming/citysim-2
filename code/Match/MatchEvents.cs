using System;
using System.Collections.Generic;

namespace CitySim.Match;

public enum MatchPhase { Lobby, RoundStart, Bid, Build, Payout, EndGame }
public sealed record PhaseChange( MatchPhase Phase, int Round, double EndsAt );
public sealed record ResourceChange( Guid Player, int Previous, int Current );
public sealed record PlacementResult( bool Success, string Reason, BoardCell Cell = null );
public sealed record CardEventResult(Guid Player,string CardId,int Coins = 0,TileClaim Claim = null);
public sealed record BoardReplacement(BoardCell Before,BoardCell After);
public sealed record CardPurchase(Guid Player,string CardId,int Price,int MarketId,int Lot);
public sealed record HandCardChange(Guid Player,string CardId);

/// <summary>Per-match notifications. Subscribers detach when their view is destroyed or rebound.</summary>
public sealed class MatchEvents
{
	public event Action<CardPurchase> CardPurchased;
	public event Action<HandCardChange> CardReceived;
	public event Action<HandCardChange> CardDiscarded;
	internal void Purchased(CardPurchase value) => CardPurchased?.Invoke(value);
	internal void Received(HandCardChange value) => CardReceived?.Invoke(value);
	internal void Discarded(HandCardChange value) => CardDiscarded?.Invoke(value);
	public event Action<PhaseChange> PhaseChanged;
	public event Action<ResourceChange> CoinsChanged;
	public event Action<ResourceChange> VpChanged;
	public event Action<Guid> HandChanged;
	public event Action<BoardCell> CardPlaced;
	public event Action<BoardReplacement> BuildingReplaced;
	public event Action<PlacementResult> PlacementRejected;
	public event Action<PayoutResult> PayoutResolved;
	public event Action<Guid> BidChanged;
	public event Action<Guid> BidStatusChanged;
	public event Action<Guid> PlayerReadyChanged;
	public event Action<int> LotsRevealed;
	public event Action<AuctionResult> AuctionResolved;
	public event Action<PlacementEffects> PlacementResolved;
	public event Action<ParkScoreResult> ParkScored;
	public event Action<GrowthResult> BuildingGrown;
	public event Action<BoardCell> CellUpdated;
	public event Action<Guid> EffectTrayChanged;
	public event Action ClaimsChanged;
	public event Action<EndScore> MatchScored;
	public event Action<CardEventResult> EventPlayed;
	public event Action PublicGoalsChanged;
	public event Action<GoalsResult> GoalsClaimed;
	public event Action<HospitalEmergencyResult> HospitalEmergency;
	public event Action<Guid> ContractChanged;
	public event Action<IReadOnlyList<ContractReveal>> ContractsRevealed;
	internal void Phase( PhaseChange change ) => PhaseChanged?.Invoke( change );
	internal void Coins( ResourceChange change ) => CoinsChanged?.Invoke( change );
	internal void Vp( ResourceChange change ) => VpChanged?.Invoke( change );
	internal void Hand( Guid player ) => HandChanged?.Invoke( player );
	internal void Placed( BoardCell cell ) => CardPlaced?.Invoke( cell );
	internal void Replaced(BoardReplacement change) => BuildingReplaced?.Invoke(change);
	internal void Rejected( PlacementResult result ) => PlacementRejected?.Invoke( result );
	internal void Payout( PayoutResult result ) => PayoutResolved?.Invoke( result );
	internal void Bid( Guid owner ) => BidChanged?.Invoke(owner);
	internal void BidStatus( Guid owner ) => BidStatusChanged?.Invoke(owner);
	internal void Ready( Guid owner ) => PlayerReadyChanged?.Invoke(owner);
	internal void Reveal( int id ) => LotsRevealed?.Invoke(id);
	internal void Auction( AuctionResult result ) => AuctionResolved?.Invoke(result);
	internal void Placement(PlacementEffects effects) => PlacementResolved?.Invoke(effects);
	internal void Park(ParkScoreResult result) => ParkScored?.Invoke(result);
	internal void Growth(GrowthResult result) => BuildingGrown?.Invoke(result);
	internal void Updated(BoardCell cell) => CellUpdated?.Invoke(cell);
	internal void Tray(Guid owner) => EffectTrayChanged?.Invoke(owner);
	internal void Claims() => ClaimsChanged?.Invoke();
	internal void Scored(EndScore result) => MatchScored?.Invoke(result);
	internal void Played(CardEventResult result) => EventPlayed?.Invoke(result);
	internal void GoalsChanged() => PublicGoalsChanged?.Invoke();
	internal void GoalClaims(GoalsResult result) => GoalsClaimed?.Invoke(result);
	internal void Emergency(HospitalEmergencyResult result) => HospitalEmergency?.Invoke(result);
	internal void Contract(Guid owner) => ContractChanged?.Invoke(owner);
	internal void RevealContracts(IReadOnlyList<ContractReveal> revealed) => ContractsRevealed?.Invoke(revealed);
}
