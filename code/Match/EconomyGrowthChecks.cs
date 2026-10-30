using System;
using System.Collections.Generic;
using System.Linq;
using CitySim.Cards;

namespace CitySim.Match;

public static class EconomyGrowthChecks
{
    static readonly Guid A = new("11111111-1111-1111-1111-111111111111"), B = new("22222222-2222-2222-2222-222222222222");
    public static IReadOnlyList<string> Run()
    {
        var passed = new List<string>();
        void Check(bool ok,string name) { if (!ok) throw new InvalidOperationException(name); passed.Add("PASS "+name); }
        MatchState Board()
        {
            var s=new MatchState(13,13,1); s.AddPlayer(A,"A"); s.AddPlayer(B,"B");
            s.SetPhase(MatchPhase.Build,1,40); return s;
        }
        // Hand-built payout fixtures intentionally isolate effects from placement adjacency and road requirements.
        BoardCell Add(MatchState s,string id,int x,int y,Guid? owner=null,int age=0)
        {
            var cell=new BoardCell(x,y,id,owner ?? A,s.Cells().Count()+1,1,age); s.UpdateCell(cell); return cell;
        }
        int Income(MatchState s,string id) => Payout.Calculate(s).Entries.Where(e=>e.Source?.CardId==id && e.Reason==CardLibrary.Get(id).Name).Sum(e=>e.Coins);
        var growth=Board(); Add(growth,"vineyard",1,1); Add(growth,"startup",3,1);
        var vineyard=new List<int>(); var startup=new List<int>(); var grown=0; growth.Events.BuildingGrown+=_=>grown++;
        for(var round=1;round<=5;round++)
        {
            growth.SetPhase(MatchPhase.Payout,round,round*40); vineyard.Add(Income(growth,"vineyard")); startup.Add(Income(growth,"startup"));
            var payout=Payout.Calculate(growth); var age=growth.Get(1,1).Age;
            Check(Payout.Calculate(growth).CoinsFor(A)==payout.CoinsFor(A) && growth.Get(1,1).Age==age,$"growth preview is pure in round {round}");
            Check(Payout.Apply(growth,payout) && !Payout.Apply(growth,payout),$"growth payout settles once in round {round}");
            if(round==2) growth=MatchState.Restore(growth.Capture() with { PlacementOrder=growth.Cells().Max(c=>c.Order) });
        }
        Check(vineyard.SequenceEqual(new[]{0,0,3,3,3}),"Vineyard waits for two completed payouts, including checkpoint restore");
        Check(startup.SequenceEqual(new[]{1,2,3,3,3}),"Startup ramps on its own payout age and caps at three");
        Check(grown==0 && !Growth.IsMature(growth.Get(1,1)),"income growth never invokes Forest growth or park awards");
        var late=Board(); late.SetPhase(MatchPhase.Build,6,300); Add(late,"startup",1,1); Add(late,"vineyard",2,1);
        Check(Income(late,"startup")==1 && Income(late,"vineyard")==0,"late growth cards start at their first payout rather than match round");

        var avenue=Board(); Add(avenue,"avenue",5,5); Add(avenue,"street",5,4); Add(avenue,"house",4,5,B);
        Check(Income(avenue,"avenue")==0,"Avenue ignores adjacent roads at the two-building threshold");
        Add(avenue,"park",6,5,B);
        Check(Income(avenue,"avenue")==1,"Avenue counts buildings of either owner and pays its owner once");
        Add(avenue,"shop",5,6);
        Check(Income(avenue,"avenue")==1,"Avenue income stays one above its threshold");
        Check(RoadScoring.Longest(avenue,A)==3,"Avenue counts double in a chain with one Street");

        var market=Board(); Add(market,"market",5,5); Add(market,"shop",3,3); Add(market,"shop",7,7,B); Add(market,"shop",2,5); Add(market,"cafe",4,5);
        Check(Income(market,"market")==1,"Market includes diagonal range two but excludes rivals, distant Shops and Cafes");
        foreach(var point in new[]{(4,4),(5,4),(6,4),(6,5)}) Add(market,"shop",point.Item1,point.Item2);
        Check(Income(market,"market")==4,"Market income caps at four qualifying owned Shops");

        var lake=Board(); Add(lake,"lake",5,5); Add(lake,"house",5,4,B); Add(lake,"apartment",4,5); Add(lake,"cottage",6,5); Add(lake,"cafe",5,6); Add(lake,"house",6,6);
        var lakeEntries=Payout.Calculate(lake).Entries.Where(e=>e.Reason=="Beside Lake").ToArray();
        Check(lakeEntries.Length==3 && lakeEntries.Count(e=>e.Player==B)==1 && lakeEntries.All(e=>e.Coins==1),"Lake pays each orthogonally adjacent residential owner once, including Apartment and Cottage");
        Check(!BoardRules.IsPark(lake.Get(5,5)) && !ParkScoring.Regions(lake).Any(),"Lake never contributes to a park region");
        Check(FinalScoring.CardEffects(lake).Single(e=>e.Reason=="Cafe").Vp==2,"Lake supplies Cafe's park end bonus");
        Check(Payout.Calculate(lake).Entries.Any(e=>e.Source?.CardId=="cottage" && e.Reason=="Beside park"),"Lake supplies Cottage's ordinary park income as well as the Lake bonus");
        Add(lake,"lake",10,10);
        Check(PlacementScoring.Preview(lake,"cottage",10,11,A)==1,"Lake counts as a park neighbour for Cottage placement");

        var orchard=Board(); Add(orchard,"orchard",5,5); Add(orchard,"farm",5,6,B); Add(orchard,"shop",7,5,B); Add(orchard,"park",4,5);
        Check(Income(orchard,"orchard")==1 && Income(orchard,"farm")==1,"adjacent Orchard and Farm independently pay their owners");
        Check(ParkScoring.Region(orchard,orchard.Get(5,5)).Sum(ParkScoring.SizeOf)==2,"Orchard also joins the adjacent park at size one");
        return passed.AsReadOnly();
    }
}
