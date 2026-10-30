using System;
using System.Collections.Generic;
using System.Linq;
using CitySim.Cards;

namespace CitySim.Match;

public static class LegendaryChecks
{
    static readonly Guid A=new("11111111-1111-1111-1111-111111111111"), B=new("22222222-2222-2222-2222-222222222222");
    public static IReadOnlyList<string> Run()
    {
        var passed=new List<string>();
        void Check(bool ok,string name) { if (!ok) throw new InvalidOperationException(name); passed.Add("PASS "+name); }
        MatchState Board() { var s=new MatchState(25,25,1); s.AddPlayer(A,"A"); s.AddPlayer(B,"B"); s.SetPhase(MatchPhase.Build,1,40); return s; }
        BoardCell Add(MatchState s,string id,int x,int y,Guid? owner=null) { var c=new BoardCell(x,y,id,owner ?? A,s.Cells().Count()+1,1); s.UpdateCell(c); return c; }
        int Station(MatchState s)=>Payout.Calculate(s).Entries.Where(e=>e.Reason=="Central Station" && e.Player==A).Sum(e=>e.Coins);
        int Senate(MatchState s)=>FinalScoring.CardEffects(s).Single(l=>l.Reason=="Senate" && l.Player==A).Vp;
        foreach (var id in new[]{"central-station","senate"}) { var c=CardLibrary.Get(id); Check(c.Rarity==Rarity.Legendary && c.NeedsRoad && c.EffectTray && c.Weight==1,$"{c.Name} metadata"); }
        var s=Board(); var station=Add(s,"central-station",0,0); Add(s,"apartment",5,5); Add(s,"shop",6,5); Add(s,"cafe",5,4); Add(s,"cafe",5,6,B); Add(s,"granary",15,15); Add(s,"shop",19,19);
        Check(Station(s)==2,"Central Station counts owned Shops independent of neighbors");
        Add(s,"market",6,6); Add(s,"shop",6,4); Add(s,"shop",7,4); Add(s,"market",8,4,B);
        Check(Station(s)==4,"Central Station counts owned Shops and Markets with a four coin cap");
        Check(Payout.Calculate(s).Entries.Single(e=>e.Reason=="Central Station").Source==station,"Central Station explains its source");
        var senate=Board(); var senateCell=Add(senate,"senate",0,0); Add(senate,"housing-authority",1,0); Add(senate,"house",2,0); Add(senate,"house",3,0,B);
        Check(Senate(senate)==0,"Senate excludes rivals and requires three buildings per tag");
        Add(senate,"cottage",4,0); Add(senate,"school",5,0);
        Check(Senate(senate)==4,"Senate counts Home-tagged Civic and its own Civic tag independently");
        Add(senate,"cafe",6,0); Add(senate,"cafe",7,0); Add(senate,"shop",8,0);
        Check(Senate(senate)==8,"Senate counts each matching tag of a multi-tag building");
        Add(senate,"factory",9,0); Add(senate,"factory",10,0); Add(senate,"factory",11,0);
        Add(senate,"park",12,0); Add(senate,"park",13,0); Add(senate,"park",14,0);
        Check(Senate(senate)==12,"Senate scores all six qualifying tags at the twelve VP cap");
        var line=FinalScoring.Calculate(senate).Players.Single(p=>p.Player==A).Lines.Single(l=>l.Reason=="Senate");
        Check(line.Vp==12 && line.Source==senateCell && line.Detail.Contains("Home"),"Senate final-score line preserves source and qualified-tag explanation");
        return passed.AsReadOnly();
    }
}
