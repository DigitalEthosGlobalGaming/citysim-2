using System;
using System.Collections.Generic;
using System.Linq;

namespace CitySim.Match;

public static class LandmarkChecks
{
    static readonly Guid A=new("11111111-1111-1111-1111-111111111111"),B=new("22222222-2222-2222-2222-222222222222");
    public static IReadOnlyList<string> Run()
    {
        var passed=new List<string>();
        void Check(bool ok,string name) { if(!ok) throw new InvalidOperationException(name); passed.Add("PASS "+name); }
        MatchState Board(MatchSettings settings=null) { var s=new MatchState(15,15,1,settings); s.AddPlayer(A,"A"); s.AddPlayer(B,"B"); s.SetPhase(MatchPhase.Build,1,40); return s; }
        void Add(MatchState s,string id,int x,int y,Guid? owner=null) => s.UpdateCell(new(x,y,id,owner??A,s.Cells().Count()+1,1));
        var stadium=Board(); Add(stadium,"stadium",5,5); Add(stadium,"house",5,4,B); Add(stadium,"apartment",4,5); Add(stadium,"cafe",6,5,B); Add(stadium,"cottage",6,6,B);
        var payout=Payout.Calculate(stadium);
        Check(payout.Entries.Count(e=>e.Reason=="Stadium · adjacent home")==2,"Stadium pays orthogonal homes of either owner, once per Apartment card");
        Check(payout.Entries.Single(e=>e.Reason=="Stadium · Cafe noise").Coins==-1,"Stadium charges adjacent Cafe owner one coin");
        Check(FinalScoring.CardEffects(stadium).Single(e=>e.Reason=="Stadium").Vp==2,"Stadium owner earns end VP for both owners' adjacent homes");
        var original=stadium.Players[B].Coins; stadium.SetPhase(MatchPhase.Payout,1,40);
        Check(Payout.Apply(stadium,payout) && stadium.Players[B].Coins==original+payout.CoinsFor(B),"signed Stadium payout explanation reconciles exactly to bank settlement");
        Check(!Payout.Apply(stadium,payout),"Stadium deductions settle only once");
        var debt=Board(new MatchSettings {StartingCoins=0,BaseIncome=0,CatchUpCoins=0});
        Add(debt,"cafe",5,5,B); Add(debt,"stadium",4,5); Add(debt,"stadium",6,5); Add(debt,"stadium",5,4); Add(debt,"stadium",5,6);
        var empty=Payout.Calculate(debt); debt.SetPhase(MatchPhase.Payout,1,40); Payout.Apply(debt,empty);
        Check(empty.CoinsFor(B)==0 && debt.Players[B].Coins==0,"Stadium deductions cannot create debt or report money that was never lost");
        var funded=Board(new MatchSettings {StartingCoins=0,BaseIncome=3}); Add(funded,"cafe",5,5,B);
        Add(funded,"stadium",4,5); Add(funded,"stadium",6,5); Add(funded,"stadium",5,4); Add(funded,"stadium",5,6);
        var fundedPayout=Payout.Calculate(funded); funded.SetPhase(MatchPhase.Payout,1,40); Payout.Apply(funded,fundedPayout);
        Check(fundedPayout.Entries.Where(e=>e.Player==B && e.Coins<0).Sum(e=>e.Coins)==-3 && funded.Players[B].Coins==0,"noise consumes simultaneous income before reaching the zero-bank floor");

        var airport=Board(); Add(airport,"airport",5,5); Add(airport,"airport",6,5,B);
        Add(airport,"house",3,3); Add(airport,"house",4,4); Add(airport,"house",5,4); Add(airport,"apartment",6,4); Add(airport,"cottage",7,5,B); Add(airport,"cafe",5,6,B); Add(airport,"house",0,0,B);
        var penalties=FinalScoring.CardEffects(airport).Where(e=>e.Reason=="Airport noise").ToArray();
        Check(penalties.Where(e=>e.Player==A).Sum(e=>e.Vp)==-3,"all Airports share a per-player penalty cap of three");
        Check(penalties.Where(e=>e.Player==B).Sum(e=>e.Vp)==-2,"overlapping Airports apply independently before the shared cap; Cafe and distant House excluded");
        Check(Payout.Calculate(airport).Entries.Where(e=>e.Reason=="Airport").Sum(e=>e.Coins)==6,"each Airport pays its owner three coins");
        Check(penalties.Any(e=>e.Detail.Contains("3,3")),"Airport range includes diagonal homes exactly two tiles away");

        var skyscraper=Board(); Add(skyscraper,"skyscraper",5,5); Add(skyscraper,"street",5,4); Add(skyscraper,"park",4,5); Add(skyscraper,"house",7,7,B); Add(skyscraper,"house",8,5);
        Check(FinalScoring.CardEffects(skyscraper).Single(e=>e.Reason=="Skyscraper").Vp==2,"Skyscraper excludes itself, roads and distant buildings, includes rivals in range");
        foreach(var p in new[]{(4,4),(6,4),(6,5),(4,6),(5,6),(6,6)}) Add(skyscraper,"park",p.Item1,p.Item2);
        Check(FinalScoring.CardEffects(skyscraper).Single(e=>e.Reason=="Skyscraper").Vp==6,"Skyscraper end bonus caps at six");
        var placement=new MatchState(11,11); placement.AddPlayer(A,"A"); placement.SeedStreet(5,6); placement.SetPhase(MatchPhase.Build,1,40); placement.GiveCard(A,"skyscraper");
        Check(!placement.TryPlace(A,"skyscraper",5,5).Success && placement.Players[A].Hand.Count==1,"Skyscraper road alone cannot satisfy its building requirement");
        placement.GiveCard(A,"park"); placement.TryPlace(A,"park",4,5);
        Check(!placement.TryPlace(A,"skyscraper",5,5).Success,"Skyscraper rejects only one adjacent building");
        placement.GiveCard(A,"park"); placement.TryPlace(A,"park",6,5);
        Check(placement.TryPlace(A,"skyscraper",5,5).Success,"Skyscraper accepts two adjacent buildings and a road");
        return passed.AsReadOnly();
    }
}
