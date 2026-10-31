using System;
using System.Collections.Generic;
using System.Linq;
using CitySim.Cards;

namespace CitySim.Match;

public static class AuctionChecks
{
    public static IReadOnlyList<string> Run()
    {
        var passed = new List<string>();
        void Check(bool ok,string name) { if (!ok) throw new InvalidOperationException(name); passed.Add($"PASS {name}"); }
        var a=Guid.NewGuid(); var b=Guid.NewGuid(); var c=Guid.NewGuid();
        var settings=new MatchSettings { EnableGoals=false,EnableContracts=false,DefaultStreets=0,DefaultCommons=0,MiniAuctionCap=2,AuctionResolveSeconds=1 };
        var state=new MatchState(11,11,77,settings); state.AddPlayer(a,"A"); state.AddPlayer(b,"B"); state.AddPlayer(c,"C");
        var rounds=new RoundMachine(state); rounds.Start(0); var view=rounds.GetAuctionView(a); var id=view.Id; var lot=view.Lots[0].Lot;
        Check(view.Lots.Count==5 && view.Lots.Select(x=>x.Lot.CardId).Distinct().Count()==5,"market reveals five different cards");
        Check(view.Lots.Any(x=>x.Lot.CardId=="street"),"Street is available in every market selection");
        Check(state.PhaseEndsAt==-1,"market remains untimed before hurry");
        Check(!rounds.BuyMarketCard(a,id+1,0,0) && !rounds.BuyMarketCard(a,id,-1,0) && !rounds.BuyMarketCard(Guid.Empty,id,0,0),"invalid, unseated and stale purchases are rejected");
        Check(rounds.BuyMarketCard(a,id,0,0) && state.Players[a].Coins==settings.StartingCoins-lot.MinimumBid && state.Players[a].Hand.Contains(lot.CardId),"purchase charges fixed price and gives card immediately");
        Check(rounds.GetAuctionView(b).Players.Single(p=>p.Player==a).SelectedLot==0,"purchase is visible in public player status");
        Check(!rounds.BuyMarketCard(a,id,1,0),"only one purchase per player per selection");
        Check(rounds.BuyMarketCard(b,id,0,0) && state.Players[b].Hand.Contains(lot.CardId),"different players may purchase the same card");
        Check(rounds.HurryAuction(a,id,1) && state.PhaseEndsAt==11 && !rounds.HurryAuction(b,id,2),"purchaser can start countdown only once");
        Check(!rounds.BuyMarketCard(c,id,1,11),"deadline rejects purchase");
        rounds.Tick(11); Check(rounds.GetAuctionView(c).Resolving && rounds.LastAuction.Wins.Count==2 && rounds.LastAuction.Wins.All(w=>w.Lot==0),"deadline passes undecided players and resolves shared purchases");
        var paid=state.Players[a].Coins; rounds.Tick(12); Check(state.Players[a].Coins==paid && rounds.GetAuctionView(a).Id!=id,"resolution does not charge purchases twice and advances selection");
        var second=rounds.GetAuctionView(a); Check(second.Lots.Select(x=>x.Lot.CardId).Distinct().Count()==second.Lots.Count,"next selection contains no duplicate faces");
        Check(rounds.Ready(a,12) && rounds.Ready(b,12) && rounds.Ready(c,12),"players can pass without paying");
        rounds.Tick(13); Check(state.Phase==MatchPhase.Build,"market cap advances to build");
        var stocked=new MatchState(5,5,8,settings with { HandTarget=1, HandLimit=5 }); stocked.AddPlayer(a,"A");
        stocked.GiveCard(a,"house"); var sr=new RoundMachine(stocked); sr.Start(0);
        var sv=sr.GetAuctionView(a); Check(sv != null && sv.Eligible && sr.BuyMarketCard(a,sv.Id,0,0),"a player above the hand target can buy a Street");
        sr.Tick(1); sv=sr.GetAuctionView(a);
        Check(sv != null && sv.Lots[0].Lot.CardId=="street" && sr.BuyMarketCard(a,sv.Id,0,1),"configured second buy can purchase another Street");
        sr.Tick(2); Check(stocked.Phase==MatchPhase.Build && stocked.Players[a].Hand.Count==3,"market ends after the configured number of buys");
        var roads=new MatchState(5,5,9,settings); roads.AddPlayer(a,"A"); roads.SeedStreet(2,2); roads.SetPhase(MatchPhase.Build,1,40); roads.GiveCard(a,"street");
        Check(!roads.TryPlace(a,"street",1,1).Success && roads.Players[a].Hand.Contains("street"),"isolated interior Street is rejected without consuming it");
        Check(roads.TryPlace(a,"street",2,1).Success,"Street can connect to a road");
        roads.GiveCard(a,"street"); Check(roads.TryPlace(a,"street",0,0).Success,"Street can start at the board edge");
        var roadside=new MatchState(6,6,10,settings); roadside.AddPlayer(a,"A"); roadside.AddPlayer(b,"B");
        roadside.UpdateCell(new(2,2,"house",b,1,1)); roadside.UpdateCell(new(2,1,"street",a,2,1)); roadside.UpdateCell(new(1,2,"avenue",a,3,1)); roadside.UpdateCell(new(3,2,"roundabout",b,4,1)); roadside.UpdateCell(new(2,3,"street",a,5,1));
        roadside.SetPhase(MatchPhase.Payout,1,1); var roadPayout=Payout.Calculate(roadside);
        Check(roadPayout.Entries.All(e=>e.Vp==0),"existing buildings beside Streets produce no recurring payout VP");
        Check(Payout.Apply(roadside,roadPayout) && !Payout.Apply(roadside,roadPayout) && roadside.Players[a].Vp==0 && roadside.Players[b].Vp==0,"payout settles once without Street VP");
        var poor=new MatchState(5,5,4,settings with {StartingCoins=0}); poor.AddPlayer(a,"A"); poor.AddPlayer(b,"B"); var pr=new RoundMachine(poor); pr.Start(0);
        var pv=pr.GetAuctionView(a); Check(!pv.Lots[0].Allowed && !pr.BuyMarketCard(a,pv.Id,0,0),"unaffordable cards cannot be purchased");
        var limited=new MatchState(5,5,5,settings); limited.AddPlayer(a,"A"); limited.GiveCard(a,"museum");
        var session=new AuctionSession(limited,1,1,new[]{new AuctionLot(0,"museum",3)});
        Check(!session.View(a).Lots[0].Allowed && !session.Buy(a,0),"ownership limits include cards in hand");
        var checkpointState=new MatchState(5,5,6,settings); checkpointState.AddPlayer(a,"A"); checkpointState.AddPlayer(b,"B"); var cr=new RoundMachine(checkpointState); cr.Start(0);
        var cv=cr.GetAuctionView(a); cr.BuyMarketCard(a,cv.Id,0,0); cr.HurryAuction(a,cv.Id,1);
        var restoredState=MatchState.Restore(checkpointState.Capture()); var restored=RoundMachine.Restore(restoredState,cr.Capture());
        Check(restoredState.PhaseEndsAt==11 && restored.GetAuctionView(b).Players.Single(p=>p.Player==a).SelectedLot==0 && restoredState.Players[a].Hand.Count==1 && restoredState.Players[a].Coins==checkpointState.Players[a].Coins,"checkpoint preserves purchases and countdown");
        passed.Add("Market checks passed.");
        return passed;
    }
}
