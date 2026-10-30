using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using CitySim.Cards;

namespace CitySim.Match;

public static class TriggerChecks
{
    static readonly Guid A = new("11111111-1111-1111-1111-111111111111"), B = new("22222222-2222-2222-2222-222222222222");
    public static IReadOnlyList<string> Run()
    {
        var passed = new List<string>();
        void Check(bool ok,string name) { if (!ok) throw new InvalidOperationException(name); passed.Add("PASS "+name); }
        MatchState Board()
        {
            var s = new MatchState(12,12,42); s.AddPlayer(A,"A"); s.AddPlayer(B,"B");
            for (var x=0;x<12;x++) s.SeedStreet(x,6);
            s.SetPhase(MatchPhase.Build,1,40); return s;
        }
        void Place(MatchState s,Guid id,string card,int x,int y)
        {
            if (!s.GiveCard(id,card) || !s.TryPlace(id,card,x,y).Success) throw new InvalidOperationException($"Setup {card} {x},{y}");
        }
        var circle=Board(); Place(circle,A,"roundabout",2,5);
        circle.GiveCard(B,"house"); var before=circle.Players[A].Vp;
        var hypothetical = new BoardCell(2,4,"house",B,100,1);
        Check(PlacementScoring.Calculate(circle,hypothetical).Awards.Any(a => a.Player==A && a.Reason=="Roundabout" && a.Vp==1) && circle.Players[A].Vp==before,"Roundabout preview awards owner without mutation");
        Check(circle.TryPlace(B,"house",2,4).Success && circle.Players[A].Vp==before+1,"rival building awards Roundabout owner");
        Place(circle,B,"street",1,5);
        Check(circle.Players[A].Vp==before+1,"adjacent road does not trigger Roundabout");
        Place(circle,A,"park",3,5);
        Check(circle.Players[A].Vp>=before+2 && PlacementScoring.Calculate(circle,new BoardCell(3,4,"street",A,101,1)).Awards.All(a => a.Reason!="Roundabout"),"own building triggers Roundabout and road preview does not");
        var contractor=Board(); Place(contractor,A,"contractor",0,5);
        var first=contractor.Players[A].Coins; contractor.GiveCard(A,"house");
        for (var i=0;i<3;i++) PlacementScoring.Preview(contractor,"house",1,5,A);
        Check(contractor.TriggerUses(contractor.Get(0,5).Order,1)==0,"previews do not consume Contractor cap");
        Check(!contractor.TryPlace(A,"house",0,5).Success && contractor.TriggerUses(contractor.Get(0,5).Order,1)==0,"rejected placement does not consume Contractor cap");
        Check(contractor.TryPlace(A,"house",1,5).Success && contractor.Players[A].Coins==first+1,"Contractor pays for own House");
        Place(contractor,B,"shop",2,5); Check(contractor.Players[A].Coins==first+1,"Contractor ignores rival Shop");
        Place(contractor,A,"apartment",3,5); Check(contractor.Players[A].Coins==first+1,"Contractor counts House card, not Apartment");
        Place(contractor,A,"shop",4,5); Place(contractor,A,"house",5,5);
        Check(contractor.Players[A].Coins==first+2 && contractor.TriggerUses(contractor.Get(0,5).Order,1)==2,"Contractor caps own House and Shop triggers at two per source per round");
        var saved=JsonSerializer.Deserialize<MatchCheckpoint>(JsonSerializer.Serialize(contractor.Capture()));
        var restored=MatchState.Restore(saved); var restoredCoins=restored.Players[A].Coins;
        Place(restored,A,"shop",6,5);
        Check(restored.Players[A].Coins==restoredCoins && restored.TriggerUses(restored.Get(0,5).Order,1)==2,"checkpoint preserves exhausted Contractor cap");
        restored.SetPhase(MatchPhase.Build,2,80); Place(restored,A,"house",7,5);
        Check(restored.Players[A].Coins==restoredCoins+1 && restored.TriggerUses(restored.Get(0,5).Order,2)==1,"Contractor cap resets in the next round after restore");
        var rejectedUsage=false;
        try { MatchState.Restore(saved with { TriggerUsage = new[] { new TriggerUsageCheckpoint(1,1,3) } }); }
        catch (ArgumentException) { rejectedUsage=true; }
        Check(rejectedUsage,"checkpoint rejects invalid Contractor usage");
        var legacy=MatchState.Restore(saved with {TriggerUsage=null});
        Check(legacy.TriggerUses(legacy.Get(0,5).Order,1)==0,"older checkpoint without trigger usage loads with empty usage");
        return passed.AsReadOnly();
    }
}
