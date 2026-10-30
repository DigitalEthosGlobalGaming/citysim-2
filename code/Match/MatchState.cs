using System;
using System.Collections.Generic;
using System.Linq;
using CitySim.Cards;

namespace CitySim.Match;

public sealed record BoardCell( int X, int Y, string CardId, Guid Owner, int Order, int PlacedRound, int Age = 0 );
public sealed record TileClaim(int X,int Y,Guid Owner,int ExpiresRound);
public sealed record RoundActivity(Guid Player,int Buildings,bool RushHour);

public sealed class MatchPlayer
{
	readonly List<string> hand = new();
	public Guid Id { get; }
	public string Name { get; }
	public int Coins { get; internal set; }
	public int Vp { get; internal set; }
	public long HandVersion { get; internal set; }
	public IReadOnlyList<string> Hand => hand.AsReadOnly();
	internal List<string> MutableHand => hand;
	internal MatchPlayer( Guid id, string name, int coins ) { Id = id; Name = name; Coins = coins; }
}

/// <summary>Authoritative rules data. Contains no engine types or scene references.</summary>
public sealed partial class MatchState
{
	readonly BoardCell[,] board;
	readonly Dictionary<Guid, MatchPlayer> players = new();
	readonly Dictionary<(int,int),TileClaim> claims = new();
	int order;
	readonly Dictionary<(int SourceOrder,int Round),int> triggerUsage = new();
	readonly Dictionary<Guid,int> roundBuildings = new();
	readonly HashSet<Guid> rushHours = new();
	public int BuildingsPlacedThisRound(Guid player) => roundBuildings.GetValueOrDefault(player);
	public bool RushHourActive(Guid player) => rushHours.Contains(player);
	internal RoundActivity[] ActivitySnapshot() => players.Keys.Select(id=>new RoundActivity(id,BuildingsPlacedThisRound(id),RushHourActive(id))).ToArray();
	void LoadActivity(IEnumerable<RoundActivity> entries)
	{
		roundBuildings.Clear(); rushHours.Clear();
		foreach (var item in entries ?? Array.Empty<RoundActivity>()) { roundBuildings[item.Player]=item.Buildings; if (item.RushHour) rushHours.Add(item.Player); }
	}
	internal int TriggerUses(int sourceOrder,int round) => triggerUsage.GetValueOrDefault((sourceOrder,round));
	internal TriggerUsageCheckpoint[] TriggerUsageSnapshot() => triggerUsage.OrderBy(p => p.Key.SourceOrder).ThenBy(p => p.Key.Round).Select(p => new TriggerUsageCheckpoint(p.Key.SourceOrder,p.Key.Round,p.Value)).ToArray();
	public int Width { get; }
	public int Height { get; }
	public int Seed { get; }
	public int Round { get; private set; }
	public MatchPhase Phase { get; private set; } = MatchPhase.Lobby;
	public double PhaseEndsAt { get; private set; }
	public MatchSettings Settings { get; }
	public MatchEvents Events { get; } = new();
	public IReadOnlyList<TileClaim> Claims => claims.Values.ToList().AsReadOnly();
	public EndScore FinalScore { get; internal set; }
	public Goals Goals { get; }
	public IReadOnlyList<ContractReveal> ContractReveals { get; internal set; } = Array.Empty<ContractReveal>();
	public int LastPaidRound { get; internal set; }
	internal int NextPlacementOrder => order+1;
	internal MatchCheckpoint Capture() => new(2,Width,Height,Seed,Settings,Round,Phase,PhaseEndsAt,order,LastPaidRound,Cells().ToArray(),Claims.ToArray(),
		players.Values.Select(p => new PlayerCheckpoint(p.Id,p.Name,p.Coins,p.Vp,p.HandVersion,p.Hand.ToArray())).ToArray(),Goals.Capture(),ContractReveals.ToArray(),FinalScore,TriggerUsageSnapshot(),ActivitySnapshot(),BuilderChoiceSnapshot());
	internal static MatchState Restore(MatchCheckpoint data)
	{
		if (data.Version != 2) throw new ArgumentException("Unsupported match checkpoint version.");
		var usage = data.TriggerUsage ?? Array.Empty<TriggerUsageCheckpoint>();
		if (usage.Any(u => u == null || u.SourceOrder <= 0 || u.SourceOrder > data.PlacementOrder || u.Round <= 0 || u.Round > data.Round || u.Count <= 0 || u.Count > 2) ||
			usage.GroupBy(u => (u.SourceOrder,u.Round)).Any(g => g.Count() != 1))
			throw new ArgumentException("Invalid placement trigger usage.");
		var s = new MatchState(data.Width,data.Height,data.Seed,data.Settings) { Round=data.Round,Phase=data.Phase,PhaseEndsAt=data.PhaseEndsAt,order=data.PlacementOrder,LastPaidRound=data.LastPaidRound,FinalScore=data.FinalScore,ContractReveals=Array.AsReadOnly(data.ContractReveals) };
		foreach (var p in data.Players) { var player = s.AddPlayer(p.Id,p.Name); player.Coins=p.Coins; player.Vp=p.Vp; player.HandVersion=p.HandVersion; player.MutableHand.AddRange(p.Hand); }
		foreach (var cell in data.Cells) s.board[cell.X,cell.Y] = cell;
		foreach (var claim in data.Claims) s.claims[(claim.X,claim.Y)] = claim;
		var activity=data.Activity ?? Array.Empty<RoundActivity>();
		if (activity.Any(a=>a==null || !s.players.ContainsKey(a.Player) || a.Buildings<0 || a.Buildings>data.PlacementOrder) || activity.Select(a=>a.Player).Distinct().Count()!=activity.Length || activity.Sum(a=>(long)a.Buildings)>data.PlacementOrder) throw new ArgumentException("Invalid round activity.");
		s.LoadActivity(activity); s.LoadBuilderChoices(data.BuilderChoices); s.Goals.Restore(data.Goals); foreach (var entry in usage) s.triggerUsage[(entry.SourceOrder,entry.Round)] = entry.Count; return s;
	}
	internal void ApplyReplica(PublicMatchSnapshot data,OwnerMatchSnapshot own)
	{
		if (data.Width != Width || data.Height != Height) throw new ArgumentException("Replica board dimensions changed.");
		var resourceChanges = new List<(ResourceChange Coins,ResourceChange Vp)>();
		var handChanges = new List<Guid>(); var placed = new List<BoardCell>(); var updated = new List<BoardCell>(); var replaced = new List<BoardReplacement>();
		var previousPhase = (Phase,Round,PhaseEndsAt); var wasScored = FinalScore != null;
		foreach (var source in data.Players)
		{
			if (!players.TryGetValue(source.Id,out var player)) player = AddPlayer(source.Id,source.Name);
			resourceChanges.Add((new(source.Id,player.Coins,source.Coins),new(source.Id,player.Vp,source.Vp)));
			player.Coins=source.Coins; player.Vp=source.Vp;
			var hand = own?.Owner == source.Id ? own.Hand : Array.Empty<string>();
			if (!player.Hand.SequenceEqual(hand)) handChanges.Add(source.Id);
			player.MutableHand.Clear(); player.MutableHand.AddRange(hand); player.HandVersion=own?.Owner == source.Id ? own.HandVersion : 0;
		}
		foreach (var cell in data.Cells)
		{
			var old = board[cell.X,cell.Y];
			if (old == null) placed.Add(cell); else if (old.Order != cell.Order || old.CardId != cell.CardId) { replaced.Add(new(old,cell)); placed.Add(cell); } else if (old != cell) updated.Add(cell);
			board[cell.X,cell.Y]=cell;
		}
		order=data.Cells.Select(c => c.Order).DefaultIfEmpty(0).Max();
		claims.Clear(); foreach (var claim in data.Claims) claims[(claim.X,claim.Y)]=claim;
		Goals.ApplyPublic(data.Goals,data.GoalHistory); ContractReveals=Array.AsReadOnly(data.ContractReveals); FinalScore=data.FinalScore;
		Phase=data.Phase; Round=data.Round; PhaseEndsAt=data.PhaseEndsAt; LoadActivity(data.Activity);
		LoadBuilderChoices(own == null ? Array.Empty<BuilderChoice>() : new[] { new BuilderChoice(own.Owner,own.BuilderCommon) });
		foreach (var change in resourceChanges) { if (change.Coins.Previous != change.Coins.Current) Events.Coins(change.Coins); if (change.Vp.Previous != change.Vp.Current) Events.Vp(change.Vp); }
		foreach (var id in handChanges) Events.Hand(id);
		foreach (var change in replaced) { Events.Replaced(change); if (CardLibrary.Get(change.Before.CardId).EffectTray) Events.Tray(change.Before.Owner); }
		foreach (var cell in placed) { Events.Placed(cell); if (CardLibrary.Get(cell.CardId).EffectTray) Events.Tray(cell.Owner); }
		foreach (var cell in updated) Events.Updated(cell);
		Events.Claims(); Events.GoalsChanged();
		if (previousPhase != (Phase,Round,PhaseEndsAt)) Events.Phase(new(Phase,Round,PhaseEndsAt));
		if (!wasScored && FinalScore != null) Events.Scored(FinalScore);
	}
	public IReadOnlyDictionary<Guid, MatchPlayer> Players => new System.Collections.ObjectModel.ReadOnlyDictionary<Guid, MatchPlayer>( players );

	public MatchState( int width, int height, int seed = 0, MatchSettings settings = null )
	{
		if ( width <= 0 || height <= 0 ) throw new ArgumentOutOfRangeException(nameof(width));
		Width = width; Height = height; Seed = seed; Settings = settings ?? new();
		if ( Settings.HandLimit < 1 || Settings.StartingCoins < 0 || Settings.RareBuildCost < 0 || Settings.LegendaryBuildCost < 0 || Settings.Rounds < 1 ||
			Settings.BuildSeconds <= 0 || !double.IsFinite(Settings.BuildSeconds) || !double.IsFinite(Settings.HospitalEmergencyFraction) || Settings.HospitalEmergencyFraction < 0 || Settings.HospitalEmergencyFraction > 1 ||
			Settings.PayoutSeconds <= 0 || !double.IsFinite(Settings.PayoutSeconds) ||
			Settings.StubBidSeconds < 0 || !double.IsFinite(Settings.StubBidSeconds) ||
			Settings.MiniAuctionSeconds <= 0 || !double.IsFinite(Settings.MiniAuctionSeconds) ||
			Settings.AuctionResolveSeconds < 0 || !double.IsFinite(Settings.AuctionResolveSeconds) ||
			Settings.HandTarget < 1 || Settings.HandTarget > Settings.HandLimit || Settings.MiniAuctionCap < 1 || Settings.ExtraLots < 1 ||
			Settings.ParkScoringCap < 1 || Settings.ParkPlacerDivisor < 1 || Settings.CoinsPerEndVp < 1 ||
			Settings.PublicGoalCount < 1 || Settings.PublicGoalCount > GoalLibrary.All.Count || Settings.ContractChoices < 1 || Settings.ContractChoices > Contracts.All.Count || Settings.GoalVp < 0 || Settings.TiedGoalVp < 0 ||
			Settings.StubOpponentBid < 0 || Settings.StubThinkSeconds < 0 || !double.IsFinite(Settings.StubThinkSeconds) ||
			Settings.AiFrameBudgetMilliseconds <= 0 || !double.IsFinite(Settings.AiFrameBudgetMilliseconds) ||
			Settings.Rarities.Count != 4 || Settings.Rarities.Select(r => r.Rarity).Distinct().Count() != 4 || Settings.Rarities.Any(r => r.MinimumBid < 1 || r.RevealWeight < 0) ) throw new ArgumentException("Invalid match settings.");
		board = new BoardCell[width,height];
		Goals = new Goals(this);
	}

	public MatchPlayer AddPlayer( Guid id, string name )
	{
		var player = new MatchPlayer( id, name, Settings.StartingCoins );
		players.Add( id, player );
		return player;
	}

	public bool InBounds( int x, int y ) => x >= 0 && y >= 0 && x < Width && y < Height;
	public BoardCell Get( int x, int y ) => InBounds(x,y) ? board[x,y] : null;
	public IEnumerable<BoardCell> Cells()
	{
		for ( var y = 0; y < Height; y++ )
		for ( var x = 0; x < Width; x++ )
			if ( board[x,y] is { } cell ) yield return cell;
	}
	public IEnumerable<BoardCell> Neighbours( int x, int y )
	{
		foreach ( var offset in new[] { (0,-1), (1,0), (0,1), (-1,0) } )
			if ( Get(x+offset.Item1,y+offset.Item2) is { } cell ) yield return cell;
	}

	public void SetPhase( MatchPhase phase, int round, double endsAt )
	{
		if ( round < 0 || round > Settings.Rounds || !double.IsFinite(endsAt) ) throw new ArgumentOutOfRangeException(nameof(round));
		if (round!=Round) { roundBuildings.Clear(); rushHours.Clear(); }
		Phase = phase; Round = round; PhaseEndsAt = endsAt;
		if (phase == MatchPhase.RoundStart)
		{
			var expired = claims.Values.Where(c => c.ExpiresRound <= round).ToArray();
			foreach (var claim in expired) claims.Remove((claim.X,claim.Y));
			if (expired.Length > 0) Events.Claims();
		}
		Events.Phase( new(phase,round,endsAt) );
	}

	public bool GiveCard( Guid playerId, string cardId )
	{
		if ( !players.TryGetValue(playerId,out var player) || !CardLibrary.TryGet(cardId,out _) || player.Hand.Count >= Settings.HandLimit ) return false;
		player.MutableHand.Add(cardId);
		HandChanged(playerId);
		Events.Received(new(playerId,cardId));
		return true;
	}

	public string PlacementError( Guid playerId, string cardId, int x, int y )
	{
		if ( Phase != MatchPhase.Build ) return "Wait for the Build phase.";
		if ( !players.TryGetValue(playerId,out var player) ) return "Unknown player.";
		if ( !CardLibrary.TryGet(cardId,out var card) ) return "Unknown card.";
		if ( !player.Hand.Contains(cardId) ) return "Card is not in your hand.";
		if ( card.Type == CardType.Event ) return "Play this event through its effect rule.";
		if ( player.Coins < Settings.BuildCost(card) ) return $"Not enough coins to build (cost: {Settings.BuildCost(card)}).";
		if ( !InBounds(x,y) ) return "Outside the board.";
		if ( Get(x,y) != null ) return "Tile is occupied.";
		if (claims.TryGetValue((x,y),out var claim) && claim.Owner != playerId && claim.ExpiresRound > Round) return "Reserved by another player.";
		if ( card.Id == "street" && x != 0 && y != 0 && x != Width-1 && y != Height-1 && !Neighbours(x,y).Any(c => CardLibrary.Get(c.CardId).Type == CardType.Road) ) return "Street needs an adjacent road or the board edge.";
		if ( card.NeedsRoad && !Neighbours(x,y).Any(c => CardLibrary.Get(c.CardId).Type == CardType.Road) ) return "Needs an adjacent road.";
		foreach (var requirement in card.Effects.Where(e => e.Hook == EffectHook.Place && e.Rule == "minimum-buildings"))
			if (Neighbours(x,y).Count(c => CardLibrary.Get(c.CardId).Type is not (CardType.Road or CardType.Event)) < requirement.Cap) return $"Needs at least {requirement.Cap} adjacent buildings.";
		if ( card.OwnershipLimit > 0 && Cells().Count(c => c.Owner == playerId && c.CardId == cardId) >= card.OwnershipLimit ) return "Ownership limit reached.";
		return null;
	}

	public PlacementResult TryPlace( Guid playerId, string cardId, int x, int y )
	{
		var error = PlacementError(playerId,cardId,x,y);
		if ( error != null ) { var rejected = new PlacementResult(false,error); Events.Rejected(rejected); return rejected; }
		var cell = new BoardCell(x,y,cardId,playerId,++order,Round);
		var effects = PlacementScoring.Calculate(this,cell);
		players[playerId].MutableHand.Remove(cardId);
		ChangeCoins(playerId,-Settings.BuildCost(CardLibrary.Get(cardId)));
		CommitPlacement(cell,effects);
		return new(true,"",cell);
	}
	internal void CommitPlacement(BoardCell cell,PlacementEffects effects,BoardCell replaced=null)
	{
		var (x,y,cardId,playerId)=(cell.X,cell.Y,cell.CardId,cell.Owner);
		board[x,y] = cell;
		if (CardLibrary.Get(cardId).Type is not (CardType.Road or CardType.Event)) roundBuildings[playerId]=BuildingsPlacedThisRound(playerId)+1;
		foreach (var award in effects.Awards)
		{
			if (award.Reason is "Contractor" or "Warehouse" && award.Source != null)
			{
				var key = (award.Source.Order,Round);
				triggerUsage[key] = triggerUsage.GetValueOrDefault(key)+1;
			}
			ApplyAward(award);
		}
		if (claims.Remove((x,y))) Events.Claims();
		HandChanged(playerId);
		if (replaced != null) { Events.Replaced(new(replaced,cell)); if (CardLibrary.Get(replaced.CardId).EffectTray) Events.Tray(replaced.Owner); }
		Events.Placed(cell);
		Events.Placement(effects);
		if (effects.Park != null) Events.Park(effects.Park);
		if (CardLibrary.Get(cardId).EffectTray) Events.Tray(playerId);
	}

	public string CardPlayError(Guid owner,string cardId,int x,int y)
	{
		if (cardId != "land-claim") return PlacementError(owner,cardId,x,y);
		if (Phase != MatchPhase.Build) return "Wait for the Build phase.";
		if (!players.TryGetValue(owner,out var player) || !player.Hand.Contains(cardId)) return "Card is not in your hand.";
		if (!InBounds(x,y)) return "Outside the board.";
		if (Get(x,y) != null) return "Tile is occupied.";
		if (claims.TryGetValue((x,y),out var claim) && claim.ExpiresRound > Round) return "Tile is already reserved.";
		return null;
	}
	public PlacementResult TryClaim(Guid owner,int slot,int x,int y)
	{
		if (!players.TryGetValue(owner,out var player) || slot < 0 || slot >= player.Hand.Count || player.Hand[slot] != "land-claim") return new(false,"Select Land Claim first.");
		var error = CardPlayError(owner,"land-claim",x,y);
		if (error != null) { var denied = new PlacementResult(false,error); Events.Rejected(denied); return denied; }
		var duration = CardLibrary.Get("land-claim").Effects.Single(e => e.Rule == "land-claim").Amount;
		claims[(x,y)] = new(x,y,owner,Round+duration);
		player.MutableHand.RemoveAt(slot); HandChanged(owner); Events.Claims();
		Events.Played(new(owner,"land-claim",Claim:claims[(x,y)]));
		return new(true,"");
	}
	internal void UpdateCell(BoardCell cell) { board[cell.X,cell.Y] = cell; Events.Updated(cell); }
	internal void ApplyAward(RuleAward award) { if (award.Coins != 0) ChangeCoins(award.Player,award.Coins); if (award.Vp != 0) ChangeVp(award.Player,award.Vp); }

	public bool Discard( Guid playerId, int slot )
	{
		if ( Phase != MatchPhase.Build || !players.TryGetValue(playerId,out var player) || slot < 0 || slot >= player.Hand.Count ) return false;
		var cardId=player.Hand[slot];
		player.MutableHand.RemoveAt(slot); HandChanged(playerId); Events.Discarded(new(playerId,cardId)); return true;
	}

	public bool PlayGrant( Guid playerId, int slot )
	{
		return players.TryGetValue(playerId,out var player) && slot>=0 && slot<player.Hand.Count && player.Hand[slot]=="grant" && PlayEvent(playerId,slot);
	}
	/// <summary>Rush Hour does not stack; a redundant copy stays in hand for a later round.</summary>
	public bool PlayEvent(Guid playerId,int slot)
	{
		if (Phase!=MatchPhase.Build || !players.TryGetValue(playerId,out var player) || slot<0 || slot>=player.Hand.Count) return false;
		var id=player.Hand[slot]; if (id is not ("grant" or "ribbon-cutting" or "rush-hour") || id=="rush-hour" && RushHourActive(playerId)) return false;
		var amount=0;
		if (id=="grant") amount=CardLibrary.Get(id).Effects.First(e=>e.Rule=="coins").Amount;
		if (id=="ribbon-cutting") { var effect=CardLibrary.Get(id).Effects.First(e=>e.Rule=="round-building-coins"); amount=(int)Math.Min(effect.Cap,(long)BuildingsPlacedThisRound(playerId)*effect.Amount); }
		if ((long)player.Coins+amount>int.MaxValue) return false;
		if (id=="rush-hour") rushHours.Add(playerId);
		player.MutableHand.RemoveAt(slot); ChangeCoins(playerId,amount); HandChanged(playerId); Events.Played(new(playerId,id,amount)); return true;
	}
	void HandChanged(Guid owner) { players[owner].HandVersion++; Events.Hand(owner); }

	/// <summary>Seed scenery before a match. Neutral roads never belong to a player.</summary>
	public bool SeedStreet( int x, int y )
	{
		if ( Phase != MatchPhase.Lobby || !InBounds(x,y) || Get(x,y) != null ) return false;
		var cell = new BoardCell(x,y,"street",Guid.Empty,++order,0);
		board[x,y] = cell; Events.Placed(cell); return true;
	}

	public bool ChangeCoins( Guid playerId, int delta )
	{
		if ( !players.TryGetValue(playerId,out var p) || (long)p.Coins + delta < 0 || (long)p.Coins + delta > int.MaxValue ) return false;
		var previous = p.Coins; p.Coins += delta;
		if ( delta != 0 ) Events.Coins(new(playerId,previous,p.Coins));
		return true;
	}
	public void ChangeVp( Guid playerId, int delta )
	{
		var p = players[playerId]; var previous = p.Vp; p.Vp = checked(p.Vp + delta);
		if ( delta != 0 ) Events.Vp(new(playerId,previous,p.Vp));
	}
}
