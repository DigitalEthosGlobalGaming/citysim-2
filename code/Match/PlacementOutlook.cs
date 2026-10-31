using System;
using System.Collections.Generic;
using System.Linq;
using CitySim.Cards;

namespace CitySim.Match;

public sealed record PlacementOutlook(IReadOnlyList<RuleAward> Awards,int NextIncome,int RemainingIncome,int PayoutsLeft,int EndVp);

public sealed partial class MatchState
{
    /// <summary>Pure placement awards and income forecast with the current surrounding buildings.
    /// Future purchases, construction, random emergencies and growth awards are not assumed.</summary>
    public PlacementOutlook PreviewOutlook(Guid owner,string cardId,int x,int y,bool replacement=false)
    {
        if(!InBounds(x,y) || !players.ContainsKey(owner) || !CardLibrary.TryGet(cardId,out var card) || card.Type==CardType.Event) return null;
        var old=board[x,y];
        if(old!=null && !replacement) return null;
        var added=new BoardCell(x,y,cardId,owner,NextPlacementOrder,Round);
        var left=Math.Max(0,Settings.Rounds-Round+1);
        int Income()=>Payout.Calculate(this).Entries.Where(e=>e.Player==owner && e.Reason is not ("Base income" or "Catch-up")).Sum(e=>e.Coins);
        int End()=>FinalScoring.CardEffects(this).Where(e=>e.Player==owner).Sum(e=>e.Vp);
        try
        {
            var endBefore=End();
            board[x,y]=null;
            var awards=PlacementScoring.Calculate(this,added).Awards;
            board[x,y]=added;
            var endDelta=End()-endBefore;
            int next=0,total=0;
            for(var t=0;t<left;t++)
            {
                board[x,y]=old==null?null:old with {Age=old.Age+t};
                var before=Income();
                board[x,y]=added with {Age=t};
                var delta=Income()-before;
                total+=delta;if(t==0) next=delta;
            }
            return new(awards,next,total,left,endDelta);
        }
        finally { board[x,y]=old; }
    }
}
