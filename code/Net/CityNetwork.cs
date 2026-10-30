using System.Text.Json;
using CitySim.Match;

namespace CitySim.Net;

internal enum NoticeKind { Placement, Park, Growth, Event, Payout, Auction, Goals, HospitalEmergency }
internal sealed record MatchNotice(NoticeKind Kind,PlacementEffects Placement=null,ParkScoreResult Park=null,GrowthResult Growth=null,
	CardEventResult CardEvent=null,PayoutResult Payout=null,AuctionResult Auction=null,GoalsResult Goals=null,HospitalEmergencyResult Emergency=null);
internal sealed record SessionPacket(LobbyView Lobby,RecipientSnapshot Match,MatchNotice[] Notices,Guid Player=default,long SequenceFloor=0);

/// <summary>Engine transport. Full rules objects exist only on the current host; RPC callers supply identity.</summary>
public sealed class CityNetwork : Component, Component.INetworkListener
{
	static CityNetwork Current => CityGame.Instance?.Session;
	CityGame city;
	MatchLobby lobby;
	MatchState hostState;
	RoundMachine hostRounds;
	MatchAuthority authority;
	BalanceAnalytics analytics;
	HostMatchVault vault;
	MatchReplica replica=new();
	MatchSettings settings=new();
	SeatDirectory seats=new();
	Guid localPlayer;
	Guid matchId;
	long revision,sequence;
	Guid sequenceMatch;
	double clock;
	bool activeBefore,publicDirty;
	float helloDelay;
	readonly HashSet<Guid> privateDirty=new(),knownActive=new();
	readonly Dictionary<Guid,long> deliveryVersions=new();
	readonly List<MatchNotice> notices=new();
	public LobbyView Lobby { get; private set; }
	public RecipientSnapshot Snapshot { get; private set; }
	public bool IsConnected => Networking.IsActive;
	public bool IsHost => IsConnected && Networking.IsHost;
	public bool IsSpectator => Snapshot?.Own==null && Snapshot!=null;
	public Guid PlayerId => localPlayer!=Guid.Empty ? localPlayer : Lobby!=null ? Guid.Empty : Connection.Local.Id;
	public AuctionView SpectatorAuction => Snapshot?.Public.Auction is { } a ? new(a.Id,a.Round,a.Number,a.Cap,PlayerId,0,0,false,false,a.Resolving,
		a.Lots.Select(l=>new OwnLotBid(l,0,false)).ToArray(),a.Players) : null;
	public string Status { get; private set; }
	public string RecoveryError { get; private set; }
	public bool CanStart => lobby?.CanStart==true && IsHost;
	public string InviteAddress => IsConnected && IsHost ? Connection.Local.SteamId.ToString() : "";
	internal MatchState HostState => hostState;
	internal RoundMachine HostRounds => hostRounds;
	public void Bind(CityGame game) { city=game; }
	public void Host()
	{
		if (IsConnected) return;
		Status="Opening lobby…"; city.NetworkChanged();
		Networking.CreateLobby(new Sandbox.Network.LobbyConfig {Name="City Sim · "+Connection.Local.DisplayName,MaxPlayers=8,Privacy=Sandbox.Network.LobbyPrivacy.FriendsOnly,DestroyWhenHostLeaves=false});
	}
	public void Join(string address)
	{
		if (IsConnected || string.IsNullOrWhiteSpace(address)) return;
		Status="Joining lobby…"; city.NetworkChanged();
		if (ulong.TryParse(address.Trim(),out var steamId)) Networking.Connect(steamId); else Networking.Connect(address.Trim());
	}
	public void Leave() { if (IsConnected) Networking.Disconnect(); }
	public void ToggleReady() { var own=Lobby?.Members.FirstOrDefault(m=>m.Id==PlayerId); if (own!=null) LobbyAction(Lobby.Id,"ready",!own.Ready); }
	public void StartMatch() { if (Lobby!=null) LobbyAction(Lobby.Id,"start",false); }
	public void Rematch() { if (Lobby!=null) LobbyAction(Lobby.Id,"rematch",false); }
	public bool Send(MatchCommandKind kind,int slot=-1,string card=null,int x=0,int y=0,int amount=0,int auctionId=0,int replacementSlot=-1)
	{
		if (!IsConnected || RecoveryError!=null || Snapshot?.Own==null) return false;
		if (sequenceMatch!=Snapshot.MatchId) { sequenceMatch=Snapshot.MatchId; sequence=0; }
		var request=new MatchCommand(Snapshot.MatchId,++sequence,Snapshot.Public.Round,Snapshot.Public.Phase,kind,auctionId,slot,Snapshot.Own.HandVersion,card,x,y,amount,replacementSlot);
		Submit(JsonSerializer.Serialize(request)); return true;
	}
	static T Parse<T>(string text,int maxLength=2_000_000)
	{
		if (string.IsNullOrEmpty(text) || text.Length>maxLength) return default;
		try { return JsonSerializer.Deserialize<T>(text); } catch (JsonException) { return default; }
	}
	[Rpc.Host]
	static void Hello()
	{
		var net=Current; if (net?.IsHost!=true || net.RecoveryError!=null) return;
		net.EnsureLobby(); var caller=Rpc.Caller ?? Connection.Local;
		net.Register(caller); net.SendTo(caller,Array.Empty<MatchNotice>());
	}
	[Rpc.Host]
	static void LobbyAction(Guid lobbyId,string action,bool ready)
	{
		var net=Current; if (net?.IsHost!=true || net.RecoveryError!=null || net.lobby?.Id!=lobbyId) return;
		var caller=Rpc.Caller ?? Connection.Local;
		var player=net.seats.Player(caller.Id);
		switch (action)
		{
			case "ready": net.lobby.Ready(player,ready); break;
			case "start": if (net.lobby.Start(player)) net.CreateMatch(); break;
			case "rematch": if (net.lobby.Rematch(player)) { net.Unhook(); net.hostState=null; net.hostRounds=null; net.authority=null; net.matchId=Guid.Empty; net.notices.Clear(); } break;
		}
		net.Flush();
	}
	[Rpc.Host]
	static void Submit(string json)
	{
		var net=Current; if (net?.IsHost!=true || net.RecoveryError!=null || net.authority==null) return;
		var caller=Rpc.Caller ?? Connection.Local;
		var player=net.seats.Player(caller.Id);
		if (net.lobby.Members.All(m=>m.Id!=player || !m.Connected || m.Spectator)) return;
		var request=Parse<MatchCommand>(json,4096); if (request==null) return;
		var reply=net.authority.Execute(player,request,net.clock);
		if (reply.Accepted) net.BeginWhenChosen();
		// Rejected requests get the latest owner state as well as a concise explanation.
		net.privateDirty.Add(player); net.Flush();
		if (!reply.Accepted) using (Rpc.FilterInclude(c=>c.Id==caller.Id)) ReceiveReply(JsonSerializer.Serialize(reply));
	}
	[Rpc.Broadcast(NetFlags.HostOnly)]
	static void ReceiveReply(string json)
	{
		if (Rpc.Caller!=null && !Rpc.Caller.IsHost) return;
		var reply=Parse<CommandReply>(json,4096); if (reply!=null) Current?.city.NetworkDenied(reply.Reason);
	}
	[Rpc.Broadcast(NetFlags.HostOnly)]
	static void ReceiveState(string json)
	{
		if (Rpc.Caller!=null && !Rpc.Caller.IsHost) return;
		var packet=Parse<SessionPacket>(json); if (packet==null) return;
		Current?.Receive(packet);
	}
	void Receive(SessionPacket packet)
	{
		var previous=Snapshot;
		localPlayer=packet.Player;
		if (packet.Match!=null)
		{
			if (sequenceMatch!=packet.Match.MatchId) { sequenceMatch=packet.Match.MatchId; sequence=packet.SequenceFloor; }
			else sequence=Math.Max(sequence,packet.SequenceFloor);
		}
		Lobby=packet.Lobby; Status=null;
		if (packet.Match==null) { Snapshot=null; city.ShowNetworkLobby(); return; }
		if (IsHost) { Snapshot=packet.Match; city.AcceptNetworkState(hostState,hostRounds,packet.Match); return; }
		// Latest private views are installed before model events make panels read them.
		Snapshot=packet.Match;
		if (!replica.Apply(PlayerId,packet.Match)) { Snapshot=previous; return; }
		clock=packet.Match.Clock; city.AcceptNetworkState(replica.State,null,packet.Match);
		var model=replica.State;
		if (previous?.MatchId==packet.Match.MatchId)
		{
			model.Events.Contract(PlayerId); model.Events.Bid(PlayerId);
			if (previous.Public.Auction?.Id!=packet.Match.Public.Auction?.Id && packet.Match.Public.Auction!=null) model.Events.Reveal(packet.Match.Public.Auction.Id);
			foreach (var p in packet.Match.Public.Players)
			{
				var before=previous.Public.Players.FirstOrDefault(v=>v.Id==p.Id);
				var nowReady=packet.Match.Public.Phase==MatchPhase.Build ? p.BuildReady : packet.Match.Public.Auction?.Players.FirstOrDefault(v=>v.Player==p.Id)?.Ready==true;
				var wasReady=previous.Public.Phase==MatchPhase.Build ? before?.BuildReady==true : previous.Public.Auction?.Players.FirstOrDefault(v=>v.Player==p.Id)?.Ready==true;
				if (nowReady && !wasReady) model.Events.Ready(p.Id);
			}
			if (model.Phase!=MatchPhase.EndGame) foreach (var notice in packet.Notices) PlayNotice(model,notice);
		}
		city.NetworkChanged();
	}
	static void PlayNotice(MatchState model,MatchNotice n)
	{
		switch(n.Kind) {
			case NoticeKind.Placement: model.Events.Placement(n.Placement); break;
			case NoticeKind.Park: model.Events.Park(n.Park); break;
			case NoticeKind.Growth: model.Events.Growth(n.Growth); break;
			case NoticeKind.Event: model.Events.Played(n.CardEvent); break;
			case NoticeKind.Payout: model.Events.Payout(n.Payout); break;
			case NoticeKind.Auction: model.Events.Auction(n.Auction); break;
			case NoticeKind.Goals: model.Events.GoalClaims(n.Goals); break;
			case NoticeKind.HospitalEmergency: model.Events.Emergency(n.Emergency); break;
		}
	}
	void EnsureLobby()
	{
		if (!IsHost || lobby!=null || RecoveryError!=null) return;
		localPlayer=seats.Bind(Connection.Local.Id,(ulong)Connection.Local.SteamId);
		lobby=new MatchLobby(Guid.NewGuid(),localPlayer); lobby.Changed+=Dirty;
		foreach (var c in Connection.All.Where(c=>c.IsActive)) Register(c);
		CreateVault(); Dirty();
	}
	void Register(Connection connection)
	{
		if (connection!=Connection.Local)
		{
			// Clients submit game commands; scene objects remain host-authoritative.
			connection.CanSpawnObjects=false; connection.CanRefreshObjects=false; connection.CanDestroyObjects=false;
		}
		var player=seats.Bind(connection.Id,(ulong)connection.SteamId);
		if (player!=Guid.Empty) lobby.Join(player,connection.DisplayName);
	}
	void CreateVault()
	{
		if (!vault.IsValid()) vault=Scene.GetAllComponents<HostMatchVault>().FirstOrDefault();
		if (!vault.IsValid()) vault=new GameObject(true,"City Sim · private host checkpoint") {NetworkMode=NetworkMode.Never}.Components.Create<HostMatchVault>();
		vault.Capture=Capture;
	}
	string Capture()
	{
		var checkpoint=hostState==null ? null : new HostCheckpoint(2,matchId,revision,hostState.Capture(),hostRounds.Capture(),clock,authority.CaptureSequences());
		return JsonSerializer.Serialize(new SessionCheckpoint(3,lobby.View(),checkpoint,settings,deliveryVersions.Select(p=>new RecipientRevision(p.Key,p.Value)).ToArray(),seats.Capture()));
	}
	void CreateMatch()
	{
		Unhook(); matchId=Guid.NewGuid(); clock=0; notices.Clear();
		var seats=lobby.Members.Where(m=>m.Connected && !m.Spectator).OrderBy(m=>m.Seat).ToArray();
		var side=Math.Clamp((int)Math.Ceiling(Math.Sqrt(settings.TilesPerPlayer*seats.Length)),settings.MinimumBoardSide,settings.MaximumBoardSide);
		hostState=new MatchState(side,side,Game.Random.Int(1,1_000_000_000),settings);
		foreach (var p in seats) hostState.AddPlayer(p.Id,p.Name);
		var random=new Random(hostState.Seed); var row=random.Next(side); var col=random.Next(side);
		for(var i=0;i<side;i++) { hostState.SeedStreet(i,row); hostState.SeedStreet(col,i); }
		hostRounds=new RoundMachine(hostState); authority=new MatchAuthority(matchId,hostState,hostRounds); Hook();
		BeginWhenChosen(); Dirty();
	}
	void BeginWhenChosen()
	{
		if (lobby.Phase==LobbyPhase.ChoosingContracts && hostRounds.ContractsChosen) { hostRounds.Start(clock); lobby.BeginPlay(); }
	}
	void Dirty() => publicDirty=true;
	void Resource(ResourceChange _) => Dirty();
	void Cell(BoardCell _) => Dirty();
	void Hand(Guid _) => Dirty();
	void Bid(Guid owner) => privateDirty.Add(owner);
	void BidStatus(Guid _) => Dirty();
	void Ready(Guid _) => Dirty();
	void Phase(PhaseChange phase) { Dirty(); if (phase.Phase==MatchPhase.EndGame) lobby.Finish(); }
	void Placement(PlacementEffects value) => notices.Add(new(NoticeKind.Placement,Placement:value));
	void Park(ParkScoreResult value) => notices.Add(new(NoticeKind.Park,Park:value));
	void Growth(GrowthResult value) => notices.Add(new(NoticeKind.Growth,Growth:value));
	void Event(CardEventResult value) => notices.Add(new(NoticeKind.Event,CardEvent:value));
	void Payout(PayoutResult value) => notices.Add(new(NoticeKind.Payout,Payout:value));
	void Auction(AuctionResult value) => notices.Add(new(NoticeKind.Auction,Auction:value));
	void Goals(GoalsResult value) => notices.Add(new(NoticeKind.Goals,Goals:value));
	void Emergency(HospitalEmergencyResult value) { notices.Add(new(NoticeKind.HospitalEmergency,Emergency:value)); Dirty(); }
	void Hook()
	{
		analytics=LocalBalanceAnalytics.Start(hostState,hostRounds,matchId,"multiplayer");
		var e=hostState.Events;
		e.PhaseChanged+=Phase; e.CoinsChanged+=Resource; e.VpChanged+=Resource; e.CardPlaced+=Cell; e.CellUpdated+=Cell; e.HandChanged+=Hand;
		e.BidChanged+=Bid; e.BidStatusChanged+=BidStatus; e.PlayerReadyChanged+=Ready; e.ContractChanged+=Ready; e.PublicGoalsChanged+=Dirty; e.ClaimsChanged+=Dirty;
		e.PlacementResolved+=Placement; e.ParkScored+=Park; e.BuildingGrown+=Growth; e.EventPlayed+=Event; e.PayoutResolved+=Payout; e.AuctionResolved+=Auction; e.GoalsClaimed+=Goals; e.HospitalEmergency+=Emergency;
	}
	void Unhook()
	{
		analytics?.Dispose(); analytics=null;
		if (hostState==null) return; var e=hostState.Events;
		e.PhaseChanged-=Phase; e.CoinsChanged-=Resource; e.VpChanged-=Resource; e.CardPlaced-=Cell; e.CellUpdated-=Cell; e.HandChanged-=Hand;
		e.BidChanged-=Bid; e.BidStatusChanged-=BidStatus; e.PlayerReadyChanged-=Ready; e.ContractChanged-=Ready; e.PublicGoalsChanged-=Dirty; e.ClaimsChanged-=Dirty;
		e.PlacementResolved-=Placement; e.ParkScored-=Park; e.BuildingGrown-=Growth; e.EventPlayed-=Event; e.PayoutResolved-=Payout; e.AuctionResolved-=Auction; e.GoalsClaimed-=Goals; e.HospitalEmergency-=Emergency;
	}
	void SendTo(Connection connection,MatchNotice[] effects)
	{
		if (!connection.IsActive) return;
		// Per-recipient counters do not expose gaps caused by another player's private bid edits.
		var player=seats.Player(connection.Id); var key=player!=Guid.Empty ? player : connection.Id;
		var delivered=deliveryVersions.GetValueOrDefault(key)+1; deliveryVersions[key]=delivered;
		var view=hostState==null ? null : MatchProjection.For(hostState,hostRounds,player,matchId,delivered,clock);
		using (Rpc.FilterInclude(c=>c.Id==connection.Id)) ReceiveState(JsonSerializer.Serialize(new SessionPacket(lobby.View(),view,effects,player,authority?.LastSequence(player) ?? 0)));
	}
	void Flush()
	{
		if (!IsHost || RecoveryError!=null || lobby==null || !publicDirty && privateDirty.Count==0) return;
		revision++; var effects=notices.ToArray();
		foreach (var c in Connection.All.Where(c=>c.IsActive && (publicDirty || privateDirty.Contains(seats.Player(c.Id))))) SendTo(c,effects);
		publicDirty=false; privateDirty.Clear(); notices.Clear();
	}
	void SettleDeparted()
	{
		if (hostState==null) return;
		foreach (var p in lobby.Members.Where(m=>!m.Connected && hostState.Players.ContainsKey(m.Id)))
		{
			if (hostState.Phase==MatchPhase.Lobby && hostRounds.GetContractView(p.Id) is {Choices.Count:>0} offer) hostRounds.ChooseContract(p.Id,offer.DraftId,offer.Choices[0]);
			else if (hostState.Phase is MatchPhase.Bid or MatchPhase.Build) hostRounds.Ready(p.Id,clock);
		}
		BeginWhenChosen();
	}
	protected override void OnUpdate()
	{
		if (city==null) city=CityGame.Instance;
		if (!city.IsValid()) return;
		if (!IsConnected)
		{
			if (activeBefore) { Unhook(); lobby=null; hostState=null; hostRounds=null; authority=null; Snapshot=null; Lobby=null; RecoveryError=null; replica=new(); seats=new(); localPlayer=Guid.Empty; knownActive.Clear(); deliveryVersions.Clear(); notices.Clear(); privateDirty.Clear(); if (vault.IsValid()) vault.GameObject.Destroy(); vault=null; city.NetworkLeft(); }
			activeBefore=false; return;
		}
		activeBefore=true;
		if (IsHost)
		{
			if (RecoveryError!=null) return;
			EnsureLobby();
			foreach (var c in Connection.All.Where(c=>c.IsActive)) if (knownActive.Add(c.Id)) { Register(c); SendTo(c,Array.Empty<MatchNotice>()); }
			if (lobby.Phase==LobbyPhase.Playing) { clock+=Time.Delta; hostRounds.Tick(clock); }
			SettleDeparted(); Flush();
		}
		else
		{
			if (Snapshot!=null && Lobby?.Phase==LobbyPhase.Playing) clock+=Time.Delta;
			if (Lobby==null && (helloDelay-=Time.Delta)<=0) { helloDelay=1; Hello(); }
		}
		city.SetNetworkClock(clock);
	}
	void Component.INetworkListener.OnActive(Connection connection) { if (IsHost && RecoveryError==null) { EnsureLobby(); Register(connection); Dirty(); } }
	void Component.INetworkListener.OnDisconnected(Connection connection) { if (IsHost) { var player=seats.Unbind(connection.Id); if (player!=Guid.Empty) lobby?.Leave(player); knownActive.Remove(connection.Id); SettleDeparted(); Dirty(); } }
	void Component.INetworkListener.OnBecameHost(Connection previousHost)
	{
		vault=Scene.GetAllComponents<HostMatchVault>().FirstOrDefault();
		if (!vault.IsValid() || vault.GameObject.NetworkMode!=NetworkMode.Never) { FailRecovery(); return; }
		var saved=Parse<SessionCheckpoint>(vault.Payload);
		MatchLobby restoredLobby; var restoredSeats=new SeatDirectory();
		MatchState restoredState=null; RoundMachine restoredRounds=null; MatchAuthority restoredAuthority=null;
		Guid restoredPlayer;
		try
		{
			SessionCheckpoint.Validate(saved);
			restoredLobby=MatchLobby.Restore(saved.Lobby); restoredSeats.Restore(saved.Seats);
			if (restoredLobby.Members.Any(m=>!saved.Seats.Any(s=>s.Player==m.Id))) throw new ArgumentException("Seat ownership is incomplete.");
			foreach (var c in Connection.All.Where(c=>c.IsActive && c.Id!=previousHost.Id && (ulong)c.SteamId!=0))
				if (restoredSeats.Bind(c.Id,(ulong)c.SteamId)==Guid.Empty) throw new ArgumentException("Active connection has no authenticated seat.");
			restoredPlayer=restoredSeats.Player(Connection.Local.Id);
			if (restoredPlayer==Guid.Empty || !restoredLobby.Members.Any(m=>m.Id==restoredPlayer && m.Connected)) throw new ArgumentException("Successor has no connected authenticated seat.");
			var previousPlayer=restoredSeats.AccountPlayer((ulong)previousHost.SteamId);
			restoredLobby.Leave(previousPlayer!=Guid.Empty ? previousPlayer : previousHost.Id);
			if (restoredLobby.Host!=restoredPlayer && !restoredLobby.Promote(restoredPlayer)) throw new ArgumentException("Successor could not be promoted.");
			if (saved.Match is { } checkpoint)
			{
				if (checkpoint.Version!=2) throw new ArgumentException("Unknown host checkpoint version.");
				restoredState=MatchState.Restore(checkpoint.Match); restoredRounds=RoundMachine.Restore(restoredState,checkpoint.Rounds);
				restoredAuthority=new MatchAuthority(checkpoint.MatchId,restoredState,restoredRounds); restoredAuthority.RestoreSequences(checkpoint.Sequences);
			}
		}
		catch (Exception e) { Log.Warning($"CitySim host recovery rejected: {e.Message}"); FailRecovery(); return; }
		Unhook(); settings=saved.Settings; lobby=restoredLobby; lobby.Changed+=Dirty; seats=restoredSeats;
		localPlayer=restoredPlayer;
		deliveryVersions.Clear(); foreach (var p in saved.Recipients ?? Array.Empty<RecipientRevision>()) deliveryVersions[p.Player]=p.Revision;
		if (saved.Match is { } m)
		{
			matchId=m.MatchId; revision=m.Revision; clock=m.Clock; hostState=restoredState; hostRounds=restoredRounds; authority=restoredAuthority; Hook();
			sequenceMatch=matchId; sequence=Math.Max(sequence,m.Sequences.FirstOrDefault(s=>s.Player==PlayerId)?.Sequence ?? 0);
		}
		CreateVault(); knownActive.Clear(); Dirty(); SettleDeparted();
		Log.Info("CitySim restored host checkpoint.");
	}
	void FailRecovery()
	{
		RecoveryError="The host checkpoint is missing or incompatible. Leave this match and start a new lobby.";
		city.Screens.Push(GameScreen.Recovery); city.NetworkChanged();
	}
	void Component.INetworkListener.OnHostChanged(Connection previousHost,Connection newHost) { helloDelay=0; Hello(); }
	protected override void OnDestroy() { Unhook(); if (lobby!=null) lobby.Changed-=Dirty; }
}
