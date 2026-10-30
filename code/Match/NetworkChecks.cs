using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace CitySim.Match;

/// <summary>Transport-independent privacy, request and handoff invariants. Real-client checks are separate.</summary>
public static class NetworkChecks
{
	static readonly Guid A=new("11111111-1111-1111-1111-111111111111"), B=new("22222222-2222-2222-2222-222222222222"), C=new("33333333-3333-3333-3333-333333333333");
	public static IReadOnlyList<string> Run()
	{
		var passed=new List<string>();
		void Check(bool condition,string label) { if (!condition) throw new InvalidOperationException(label); passed.Add("PASS "+label); }
		string Json<T>(T value) => JsonSerializer.Serialize(value);
		T Copy<T>(T value) => JsonSerializer.Deserialize<T>(Json(value));
		MatchState Lobby(MatchSettings settings=null) { var s=new MatchState(11,11,732948189,settings ?? new MatchSettings {EnableContracts=false,DefaultCommons=0,DefaultStreets=0}); s.AddPlayer(A,"A"); s.AddPlayer(B,"B"); s.AddPlayer(C,"C"); s.SeedStreet(5,5); return s; }
		(MatchState,RoundMachine) Clone(MatchState state,RoundMachine rounds)
		{
			var data=Copy(new HostCheckpoint(2,A,19,state.Capture(),rounds.Capture(),7,Array.Empty<CommandSequence>()));
			var restored=MatchState.Restore(data.Match); return (restored,RoundMachine.Restore(restored,data.Rounds));
		}
		var lobby=Lobby(new MatchSettings {DefaultCommons=0,DefaultStreets=0}); var machine=new RoundMachine(lobby);
		lobby.GiveCard(A,"house"); lobby.GiveCard(A,"farm"); lobby.GiveCard(B,"monument"); lobby.GiveCard(B,"grant"); lobby.GiveCard(C,"bank");
		var offered=machine.GetContractView(B); machine.ChooseContract(B,offered.DraftId,offered.Choices[0]);
		var matchId=Guid.NewGuid(); var a=MatchProjection.For(lobby,machine,A,matchId,1,0); var b=MatchProjection.For(lobby,machine,B,matchId,1,0);
		Check(a.Own.Owner==A && a.Own.Hand.SequenceEqual(new[] {"house","farm"}) && b.Own.Hand.SequenceEqual(new[] {"monument","grant"}),"each recipient gets exactly their own hand");
		Check(!Json(a.Public).Contains("monument") && !Json(a.Public).Contains("grant") && !Json(a.Public).Contains("bank"),"public lobby payload contains none of the private hand card ids");
		Check(a.Public.Players.Select(p=>p.HandCount).SequenceEqual(new[] {2,2,1}),"public players expose hand counts only");
		Check(a.Own.Contract.Selected==null && b.Own.Contract.Selected==offered.Choices[0] && a.Public.ContractReveals.Length==0,"kept contracts stay in their owner's projection");
		Check(a.Own.Contract.DraftId==matchId.GetHashCode() && !Json(a).Contains(lobby.Seed.ToString()) && !Json(a.Public).Contains("RandomState") && !Json(a.Public).Contains("Deck"),"wire projections use an independent match token and omit the secret seed and shuffle states");
		Check(MatchProjection.For(lobby,machine,Guid.Empty,matchId,1,0).Own==null,"spectator gets no owner payload");
		var replica=new MatchReplica(); Check(replica.Apply(A,Copy(a)) && replica.State.Seed==0 && replica.State.Players[B].Hand.Count==0 && replica.State.Players[C].Hand.Count==0,"deserialized client model has no rival hands or authoritative seed");
		Check(!replica.Apply(A,b with {Revision=2}) && replica.Latest.Revision==1,"owner projection cannot be applied to a different recipient");
		Check(!replica.Apply(A,a) && !replica.Apply(A,a with {Revision=0}),"duplicate and older snapshots cannot roll back a replica");
		var replacement=a with {MatchId=Guid.NewGuid(),Revision=0}; Check(replica.Apply(A,replacement) && !replica.Apply(A,a with {Revision=100}),"retired match snapshots cannot resurrect a previous game");
		Check(!replica.Apply(A,replacement with {Version=99,Revision=1}) && !replica.Apply(A,replacement with {Revision=1,Clock=double.NaN}),"unsupported and nonfinite snapshots are rejected");
		var clonedLobby=Clone(lobby,machine); Check(Json(clonedLobby.Item1.Capture())==Json(lobby.Capture()) && Json(clonedLobby.Item2.Capture())==Json(machine.Capture()),"JSON checkpoint round trip preserves lobby hands, offers, selections and goal deck");
		var contractAuthority=new MatchAuthority(matchId,lobby,machine);
		var pick=new MatchCommand(matchId,1,0,MatchPhase.Lobby,MatchCommandKind.KeepContract,AuctionId:matchId.GetHashCode()^1,CardId:a.Own.Contract.Choices[0]);
		Check(!contractAuthority.Execute(A,pick,0).Accepted,"stale contract draft token cannot select an otherwise offered card");
		Check(contractAuthority.Execute(A,pick with {Sequence=2,AuctionId=matchId.GetHashCode()},0).Accepted,"opaque current draft token selects the owner's offered contract");

		var state=Lobby(); var rounds=new RoundMachine(state); rounds.Start(0);
		var authority=new MatchAuthority(matchId,state,rounds);
		MatchCommand Command(Guid id,long sequence,MatchCommandKind kind,int slot=-1,string card=null,long? handVersion=null,int auction=0,int amount=0) => new(id,sequence,state.Round,state.Phase,kind,auction,slot,handVersion ?? state.Players[A].HandVersion,card,5,4,amount);
		var auctionId=rounds.GetAuctionView(A).Id;
		var firstLot=rounds.GetAuctionView(A).Lots[0].Lot;
		Check(authority.Execute(A,Command(matchId,1,MatchCommandKind.BuyMarketCard,0,auction:auctionId),0).Accepted,"authenticated player can buy a market card");
		var bidA=MatchProjection.For(state,rounds,A,matchId,2,0); var bidB=MatchProjection.For(state,rounds,B,matchId,2,0);
		Check(bidA.Own.Hand.Contains(firstLot.CardId) && bidB.Public.Auction.Players.Single(p=>p.Player==A).SelectedLot==0,"purchase and selected card are publicly visible");
		Check(Json(bidA.Public)==Json(bidB.Public) && bidB.Own.Hand.Length==0,"public market is identical while hands remain private");
		Check(!authority.Execute(A,Command(matchId,1,MatchCommandKind.BuyMarketCard,0,auction:auctionId),0).Accepted,"replayed purchase cannot charge twice");
		Check(!authority.Execute(Guid.Empty,Command(matchId,2,MatchCommandKind.BuyMarketCard,0,auction:auctionId),0).Accepted,"unseated connection cannot purchase");
		Check(!authority.Execute(A,Command(Guid.NewGuid(),2,MatchCommandKind.BuyMarketCard,0,auction:auctionId),0).Accepted,"prior match id cannot purchase");
		Check(!authority.Execute(A,Command(matchId,2,MatchCommandKind.BuyMarketCard,0,auction:auctionId+1),0).Accepted,"stale market id is rejected");
		Check(!authority.Execute(A,Command(matchId,3,MatchCommandKind.BuyMarketCard,1,auction:auctionId),0).Accepted,"a second purchase in the same selection is rejected");
		Check(!authority.Execute(A,Command(matchId,4,MatchCommandKind.Ready,auction:auctionId) with {Round=2},0).Accepted,"stale phase cannot pass");
		Check(!authority.Execute(Guid.Empty,Command(matchId,5,MatchCommandKind.HurryAuction,auction:auctionId),0).Accepted,"spectator cannot start the countdown");
		Check(authority.Execute(B,Command(matchId,1,MatchCommandKind.HurryAuction,auction:auctionId),0).Accepted && state.PhaseEndsAt==10,"authenticated player starts countdown");
		Check(!authority.Execute(A,Command(matchId,5,MatchCommandKind.BuyMarketCard,0,auction:auctionId),state.PhaseEndsAt).Accepted,"deadline rejects late purchases");
		Check(authority.Execute(C,Command(matchId,1,MatchCommandKind.BuyMarketCard,0,auction:auctionId,amount:0),1).Accepted && state.Players[C].Hand.Contains(firstLot.CardId),"multiple authenticated players can buy the same card at the host price");
		var locked=Clone(state,rounds);
		Check(locked.Item2.GetAuctionView(A).Ready && locked.Item2.GetAuctionView(A).Players.Single(p=>p.Player==A).SelectedLot==0 && locked.Item1.Players[A].Coins==state.Players[A].Coins,"purchase and deadline survive checkpoint restoration");
		foreach (var owner in new[] {B,C}) { rounds.Ready(owner,0); locked.Item2.Ready(owner,0); }
		Check(Json(state.Capture())==Json(locked.Item1.Capture()) && Json(rounds.Capture())==Json(locked.Item2.Capture()),"restored market resolves without charging purchases twice");
		var resolving=Clone(state,rounds); Check(resolving.Item2.GetAuctionView(A).Resolving,"auction results interval survives migration");
		for (var step=0;step<12 && state.Phase==MatchPhase.Bid;step++) { if (state.PhaseEndsAt < 0) foreach (var owner in new[] {A,B,C}) { rounds.Ready(owner,0); resolving.Item2.Ready(owner,0); } var time=state.PhaseEndsAt; rounds.Tick(time); resolving.Item2.Tick(time); }
		Check(state.Phase==MatchPhase.Build && Json(state.Capture())==Json(resolving.Item1.Capture()) && Json(rounds.Capture())==Json(resolving.Item2.Capture()),"post-handoff draw sequence and mini-auction counters remain identical");
		state.GiveCard(A,"house"); state.GiveCard(A,"house"); var hand=state.Players[A].HandVersion; var slot=state.Players[A].Hand.ToList().IndexOf("house");
		var place=Command(matchId,6,MatchCommandKind.Place,slot,"house",hand);
		Check(authority.Execute(A,place,0).Accepted && state.Get(5,4)?.Owner==A,"placement request consumes the authenticated owner's selected card");
		var count=state.Players[A].Hand.Count;
		Check(!authority.Execute(A,place,0).Accepted && state.Players[A].Hand.Count==count,"retransmitted placement cannot consume a second copy");
		Check(!authority.Execute(A,place with {Sequence=7,X=4,Y=5},0).Accepted && state.Get(4,5)==null,"old hand version cannot consume an identical card after a slot shift");
		Check(!authority.Execute(A,place with {Sequence=8,HandVersion=state.Players[A].HandVersion,CardId="monument"},0).Accepted,"client cannot substitute an unowned card id");
		var hostDenials=0; state.Events.PlacementRejected+=_=>hostDenials++;
		var occupied=authority.Execute(A,place with {Sequence=9,HandVersion=state.Players[A].HandVersion},0);
		Check(!occupied.Accepted && occupied.Reason.Contains("occupied") && hostDenials==0,"network placement denial is returned to its caller without firing the host's local denial feedback");
		rounds.Ready(A,0); var build=Clone(state,rounds);
		Check(build.Item2.IsBuildReady(A) && !build.Item2.IsBuildReady(B),"Build-ready seats restore without auto-readying rivals");
		Check(!authority.Execute(A,place with {Sequence=10,HandVersion=state.Players[A].HandVersion,X=4,Y=5},0).Accepted,"ready player cannot continue building through RPC");
		foreach (var owner in new[] {B,C}) { rounds.Ready(owner,0); build.Item2.Ready(owner,0); }
		Check(state.Phase==MatchPhase.Payout && Json(state.Capture())==Json(build.Item1.Capture()),"restored Build readiness reaches the same single payout");
		var payout=Clone(state,rounds); var paid=state.Players[A].Coins;
		Check(!Payout.Apply(payout.Item1,payout.Item2.LastPayout) && payout.Item1.Players[A].Coins==paid,"restored payout checkpoint cannot pay the same round twice");
		var late=MatchProjection.For(state,rounds,B,matchId,50,3); var lateReplica=new MatchReplica(); lateReplica.Apply(B,Copy(late));
		Check(lateReplica.State.Round==1 && lateReplica.State.Phase==MatchPhase.Payout && lateReplica.Latest.Clock==3 && lateReplica.State.Cells().SequenceEqual(state.Cells()),"late-join snapshot restores board, current phase, round and clock");
		Check(lateReplica.State.Players[B].Hand.SequenceEqual(state.Players[B].Hand) && lateReplica.State.Players[A].Hand.Count==0,"late join restores only the joining owner's hand");
		for (var step=0;step<150 && state.Phase!=MatchPhase.EndGame;step++) { if (state.PhaseEndsAt < 0) foreach (var owner in new[] {A,B,C}) { rounds.Ready(owner,0); payout.Item2.Ready(owner,0); } var time=state.PhaseEndsAt; rounds.Tick(time); payout.Item2.Tick(time); }
		Check(state.Phase==MatchPhase.EndGame && Json(state.Capture())==Json(payout.Item1.Capture()) && Json(rounds.Capture())==Json(payout.Item2.Capture()),"eight-round migration replay preserves every future draw, goal, payout and final score");
		var end=Clone(state,rounds); var finalVp=end.Item1.Players[A].Vp; end.Item2.Tick(100000);
		Check(Json(end.Item1.FinalScore)==Json(state.FinalScore) && end.Item1.Players[A].Vp==finalVp,"final score cache survives a handoff without duplicate bonuses");
		var sequences=authority.CaptureSequences(); var resumedAuthority=new MatchAuthority(matchId,end.Item1,end.Item2); resumedAuthority.RestoreSequences(Copy(sequences));
		Check(!resumedAuthority.Execute(A,place with {Sequence=9,Round=8,Phase=MatchPhase.EndGame},0).Accepted,"request replay protection survives authority restoration");
		var invalid=false; try { MatchState.Restore(state.Capture() with {Version=99}); } catch (ArgumentException) { invalid=true; }
		Check(invalid,"unsupported host checkpoint version cannot be restored");
		var random=new MatchRandom(101); for (var i=0;i<99;i++) random.Next(17); var resume=new MatchRandom(0) {State=random.State};
		Check(Enumerable.Range(0,1000).All(_=>random.Next(int.MaxValue)==resume.Next(int.MaxValue)),"explicit RNG state reproduces the next thousand draws");
		var directory=new SeatDirectory(); var d=Guid.NewGuid(); var e=Guid.NewGuid();
		Check(directory.Bind(A,101)==A && directory.Bind(B,202)==B,"authenticated accounts establish distinct game seats");
		Check(directory.Bind(Guid.Empty,101)==Guid.Empty && directory.Bind(d,0)==Guid.Empty,"empty connection or unauthenticated account cannot own a seat");
		Check(directory.Bind(A,303)==Guid.Empty && directory.AccountPlayer(303)==Guid.Empty && directory.Player(A)==A,"connection identity cannot switch accounts or mutate the ownership registry");
		Check(directory.Bind(d,101)==A && directory.Player(d)==A && directory.Player(A)==Guid.Empty,"new connection for the same account recovers the existing seat and revokes the old binding");
		Check(directory.Unbind(A)==Guid.Empty && directory.Player(d)==A,"late disconnect from the old connection cannot disconnect the replacement");
		Check(directory.Unbind(d)==A && directory.AccountPlayer(101)==A,"disconnect drops the live channel while retaining authenticated seat ownership");
		Check(directory.Bind(e,101)==A && directory.Player(e)==A,"a returning account retains its seat across repeated connection changes");
		var savedSeats=Copy(directory.Capture()); var restoredSeats=new SeatDirectory(); restoredSeats.Restore(savedSeats);
		Check(restoredSeats.AccountPlayer(101)==A && restoredSeats.AccountPlayer(202)==B && restoredSeats.Player(e)==Guid.Empty,"host checkpoint restores ownership without trusting stale connection bindings");
		var replacementChannel=Guid.NewGuid(); Check(restoredSeats.Bind(replacementChannel,202)==B && restoredSeats.Player(replacementChannel)==B,"successor binds a new authenticated channel to the saved rival seat");
		Check(restoredSeats.Bind(Guid.NewGuid(),404)!=A && restoredSeats.AccountPlayer(404)!=B,"a different authenticated account cannot recover another player's seat");
		var badSeats=false; try { new SeatDirectory().Restore(new[] {new SeatAccount(101,A),new SeatAccount(202,A)}); } catch (ArgumentException) {badSeats=true;}
		Check(badSeats,"duplicate seat ownership in a host checkpoint is rejected");
		Check(resumedAuthority.LastSequence(A)==authority.LastSequence(A),"reconnected owner can resume after its restored command sequence floor");
		var room=new MatchLobby(Guid.NewGuid(),A,3); var notifications=0; room.Changed+=()=>notifications++;
		room.Join(A,"A"); room.Join(B,"B"); room.Join(C,"C");
		Check(room.Members.Count==3 && room.Members.All(m=>!m.Spectator) && !room.CanStart,"three lobby seats join unready");
		Check(!room.Join(A,"Other A") && !room.Join(Guid.Empty,"Nobody"),"duplicate and empty lobby identities are rejected");
		room.Ready(A,true); room.Ready(B,true); Check(!room.CanStart && !room.Start(A),"host start waits for every seated player's readiness");
		room.Ready(C,true); Check(room.CanStart && !room.Start(B),"ready nonhost cannot start the lobby");
		Check(room.Start(A) && !room.Ready(C,false) && !room.Start(A),"host starts contract selection exactly once and freezes readiness");
		var observer=Guid.NewGuid(); room.Join(observer,"Observer");
		Check(room.Members.Single(m=>m.Id==observer).Spectator && !room.Ready(observer,true),"late joiner observes the active match without taking a seat or readying it");
		Check(room.BeginPlay() && !room.BeginPlay(),"contract selection advances to play once");
		var roomCopy=MatchLobby.Restore(Copy(room.View())); Check(Json(roomCopy.View())==Json(room.View()),"public lobby checkpoint preserves seats, readiness, host and phase");
		var session=new SessionCheckpoint(3,room.View(),new HostCheckpoint(2,matchId,1,state.Capture(),rounds.Capture(),1,sequences),new MatchSettings(),Seats:directory.Capture());
		bool Rejected(SessionCheckpoint candidate) { try { SessionCheckpoint.Validate(candidate); return false; } catch (ArgumentException) { return true; } }
		SessionCheckpoint.Validate(session with {Lobby=session.Lobby with {Phase=LobbyPhase.Finished}});
		Check(Rejected(session with {Settings=null}) && Rejected(session with {Match=null}) && Rejected(session with {Lobby=session.Lobby with {Phase=LobbyPhase.Waiting}}),"incomplete and contradictory session checkpoints are rejected");
		Check(Rejected(session with {Match=session.Match with {Clock=double.NaN}}) && Rejected(session with {Match=session.Match with {MatchId=Guid.Empty}}) && Rejected(session with {Match=session.Match with {Sequences=null}}),"invalid checkpoint clock, match identity and command sequences are rejected");
		room.Promote(B); room.Leave(A);
		Check(room.Host==B && room.Members.Single(m=>m.Id==A).Connected==false && room.Members.Single(m=>m.Id==A).Seat==0,"migration promotes the engine's successor and retains the departed player's match seat");
		var reconnect=MatchLobby.Restore(room.View());
		Check(reconnect.Join(A,"A rejoined") && reconnect.Members.Single(m=>m.Id==A) is {Connected:true,Seat:0,Spectator:false},"same authenticated identity can reconnect to its existing active-match seat");
		Check(!room.Promote(A) && !room.Leave(A),"departed player cannot be promoted or disconnected twice");
		Check(room.Finish() && !room.Rematch(C) && room.Rematch(B),"only the host can return a completed match to ready-up");
		Check(room.Members.Count==3 && room.Members.All(m=>!m.Ready && !m.Spectator) && room.Members.Any(m=>m.Id==observer),"rematch frees disconnected seats and admits the waiting observer");
		Check(notifications>0 && room.Revision==notifications,"lobby mutations emit one revision and event apiece");
		var charged=Lobby(new MatchSettings { EnableContracts=false,DefaultCommons=0,DefaultStreets=0,RareBuildCost=2,LegendaryBuildCost=4 }); var chargedRounds=new RoundMachine(charged);
		charged.SetPhase(MatchPhase.Build,1,100); charged.GiveCard(A,"bank"); charged.GiveCard(A,"bank");
		var chargedId=Guid.NewGuid(); var chargedAuthority=new MatchAuthority(chargedId,charged,chargedRounds);
		var paidBuild=new MatchCommand(chargedId,1,1,MatchPhase.Build,MatchCommandKind.Place,Slot:0,HandVersion:charged.Players[A].HandVersion,CardId:"bank",X:5,Y:4);
		Check(chargedAuthority.Execute(A,paidBuild,0).Accepted && charged.Players[A].Coins==6,"authenticated Rare build charges exactly two coins");
		Check(!chargedAuthority.Execute(A,paidBuild,0).Accepted && !chargedAuthority.Execute(A,paidBuild with {Sequence=2,X=4,Y=5},0).Accepted && charged.Players[A].Coins==6,"replayed sequence and stale hand cannot charge another build");
		var paidSnapshot=MatchProjection.For(charged,chargedRounds,A,chargedId,1,0); var paidReplica=new MatchReplica();
		Check(paidReplica.Apply(A,Copy(paidSnapshot)) && paidReplica.State.Players[A].Coins==6 && paidReplica.State.Settings.BuildCost(Cards.CardLibrary.Get("bank"))==2,"replica retains paid balance and build-cost settings");
		var restoredPaid=Clone(charged,chargedRounds); var restoredAuthority=new MatchAuthority(chargedId,restoredPaid.Item1,restoredPaid.Item2);
		restoredAuthority.RestoreSequences(Copy(chargedAuthority.CaptureSequences()));
		Check(!restoredAuthority.Execute(A,paidBuild,0).Accepted && restoredPaid.Item1.Players[A].Coins==6,"migration retains charged balance and rejects replay without charging");
		charged.ChangeCoins(A,-6); var cannotAfford=paidBuild with {Sequence=3,HandVersion=charged.Players[A].HandVersion,X=4,Y=5};
		Check(!chargedAuthority.Execute(A,cannotAfford,0).Accepted && charged.Players[A].Coins==0 && charged.Players[A].Hand.Count==1 && charged.Get(4,5)==null,"network build denied for insufficient coins leaves card, balance and board intact");
		return passed.AsReadOnly();
	}
}
