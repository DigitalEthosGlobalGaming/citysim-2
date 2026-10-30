using System;
using System.Collections.Generic;
using System.Linq;
using CitySim.Cards;

namespace CitySim.Match;

public static class CivicChecks
{
    static readonly Guid A = new("11111111-1111-1111-1111-111111111111"), B = new("22222222-2222-2222-2222-222222222222");
    public static IReadOnlyList<string> Run()
    {
        var passed=new List<string>();
        void Check(bool ok,string name) { if (!ok) throw new InvalidOperationException(name); passed.Add("PASS "+name); }
        MatchState Board() { var s=new MatchState(25,25,1); s.AddPlayer(A,"A"); s.AddPlayer(B,"B"); s.SetPhase(MatchPhase.Build,1,40); return s; }
        BoardCell Add(MatchState s,string id,int x,int y,Guid? owner=null) { var cell=new BoardCell(x,y,id,owner ?? A,s.Cells().Count()+1,1); s.UpdateCell(cell); return cell; }
        int Income(MatchState s,string reason) => Payout.Calculate(s).Entries.Where(e=>e.Player==A && e.Reason==reason).Sum(e=>e.Coins);
        foreach(var id in new[]{"tourist-office","tax-office","depot","school","granary"})
        {
            var card=CardLibrary.Get(id);
            Check(card.Type==CardType.Civic && card.EffectTray && card.OwnershipLimit==1 && card.Weight==2,$"{card.Name} is a one-per-player Civic tray card");
        }
        var tourism=Board(); Add(tourism,"tourist-office",1,1); Add(tourism,"cafe",3,1,B);
        Check(Income(tourism,"Tourist Office")==1,"Tourist Office counts itself and excludes rival Culture");
        Add(tourism,"cafe",4,1); Add(tourism,"museum",5,1); Add(tourism,"fountain",6,1);
        Check(Income(tourism,"Tourist Office")==3,"Tourist Office caps Culture income at three");
        var tax=Board(); Add(tax,"tax-office",1,1); Add(tax,"housing-authority",2,1); Add(tax,"house",3,1); Add(tax,"apartment",4,1); Add(tax,"cottage",5,1); Add(tax,"house",6,1,B);
        Check(Income(tax,"Tax Office")==1,"Tax Office counts Housing Authority and excludes rival Homes");
        Add(tax,"house",7,1); Check(Income(tax,"Tax Office")==1,"Tax Office counts residential houses once, including Cottage and Apartment");
        var depot=Board(); Add(depot,"depot",1,1); Add(depot,"avenue",2,1); for(var x=3;x<6;x++) Add(depot,"street",x,1);
        Add(depot,"street",6,1,B); Check(Income(depot,"Depot")==0,"Depot counts owned physical road tiles, not rival roads or Avenue weight");
        Add(depot,"roundabout",7,1); Check(Income(depot,"Depot")==1,"Depot pays one per five owned roads");
        for(var x=0;x<20;x++) Add(depot,"street",x,3); Check(Income(depot,"Depot")==4,"Depot caps income at four");
        var school=Board(); Add(school,"school",10,10); Add(school,"house",8,8); Add(school,"cottage",12,12); Add(school,"apartment",11,10); Add(school,"housing-authority",9,9); Add(school,"house",10,8,B); Add(school,"house",13,10);
        Check(Income(school,"School")==4,"School includes Housing Authority and diagonal range while excluding rivals");
        Add(school,"house",10,11); Add(school,"house",11,11); Check(Income(school,"School")==4,"School caps at four houses");
        var granary=Board(); Add(granary,"granary",0,0); Add(granary,"farm",5,5); Add(granary,"orchard",6,5); Add(granary,"farm",7,5,B);
        Check(Income(granary,"Granary")==2,"Granary counts owned Farms and Orchards, excluding rivals");
        Add(granary,"farm",8,5); Add(granary,"farm",9,5);
        Check(Income(granary,"Granary")==3,"Granary caps owned farm income at three");
        return passed.AsReadOnly();
    }
}
