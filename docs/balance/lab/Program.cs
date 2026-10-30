using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using CitySim.Ai;
using CitySim.Cards;
using CitySim.Match;

// Research runner only. Compiles the current pure rules; never changes the game's defaults.
var output = args.Length > 0 ? Path.GetFullPath(args[0]) : throw new ArgumentException("Supply an output JSON path.");
var matches = args.Length > 1 ? int.Parse(args[1]) : 32;
var seed = args.Length > 2 ? int.Parse(args[2]) : 30102026;
if (matches < 4 || matches > 10000 || matches % 4 != 0) throw new ArgumentException("Use complete four-seat rotations.");
var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../.."));
var sourceFiles = Directory.GetFiles(Path.Combine(root,"code"),"*.cs",SearchOption.AllDirectories)
    .Where(p => new[]{"Cards","Match","Ai"}.Contains(Path.GetRelativePath(Path.Combine(root,"code"),p).Split(Path.DirectorySeparatorChar)[0]))
    .OrderBy(p=>p,StringComparer.Ordinal).ToArray();
var fingerprints = sourceFiles.ToDictionary(p=>Path.GetRelativePath(root,p).Replace('\\','/'),p=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))));
var variants = new (string Name, MatchSettings Settings)[] {
    ("current", new()),
    ("base-income-1", new() { BaseIncome=1 }),
    ("prices-1-3-5-8", new() { Rarities=Array.AsReadOnly(new[]{new RaritySettings(Rarity.Common,1,55,1),new RaritySettings(Rarity.Uncommon,3,28,1),new RaritySettings(Rarity.Rare,5,14,2),new RaritySettings(Rarity.Legendary,8,3,4)}) }),
    ("coins-per-vp-3", new() { CoinsPerEndVp=3 })
};
if (args.Length > 3 && args[3] == "prototype-a")
    variants = new (string Name, MatchSettings Settings)[] { ("prototype-a", new() { RareBuildCost=2, LegendaryBuildCost=4, Rarities=Array.AsReadOnly(new[]{new RaritySettings(Rarity.Common,1,55,1),new RaritySettings(Rarity.Uncommon,3,28,1),new RaritySettings(Rarity.Rare,5,14,2),new RaritySettings(Rarity.Legendary,8,3,4)}) }) };
if (args.Length > 3 && args[3] == "final")
    variants = new (string Name, MatchSettings Settings)[] { ("final", new()) };
if (args.Length > 3 && args[3] == "pricing-comparison")
    variants = new (string Name, MatchSettings Settings)[] {
        ("prototype-b", new() { RareBuildCost=0, LegendaryBuildCost=0, Rarities=Array.AsReadOnly(new[]{new RaritySettings(Rarity.Common,1,55,1),new RaritySettings(Rarity.Uncommon,4,28,1),new RaritySettings(Rarity.Rare,7,14,2),new RaritySettings(Rarity.Legendary,11,3,4)}) }),
        ("pricing-candidate", new() { RareBuildCost=0, LegendaryBuildCost=0, Rarities=Array.AsReadOnly(new[]{new RaritySettings(Rarity.Common,2,55,1),new RaritySettings(Rarity.Uncommon,5,28,1),new RaritySettings(Rarity.Rare,9,14,2),new RaritySettings(Rarity.Legendary,14,3,4)}) })
    };
var runs = new List<object>();
foreach (var variant in variants)
{
    var outcomes = new List<object>();
    var cards = CardLibrary.All.ToDictionary(c=>c.Id,_=>new CardTotals());
    for (var m=0;m<matches;m++)
    {
        var matchSeed=unchecked(seed+(m/4)*104729);
        var s=variant.Settings;
        var side=Math.Clamp((int)Math.Ceiling(Math.Sqrt(s.TilesPerPlayer*4)),s.MinimumBoardSide,s.MaximumBoardSide);
        var state=new MatchState(side,side,matchSeed,s);
        var ids=Enumerable.Range(1,4).Select(i=>new Guid($"00000000-0000-0000-0000-{i:000000000000}")).ToArray();
        foreach(var id in ids) state.AddPlayer(id,"Seat");
        var (row,column)=StartingRoads.Choose(side,side,matchSeed);
        for(var x=0;x<side;x++) state.SeedStreet(x,row);
        for(var y=0;y<side;y++) state.SeedStreet(column,y);
        var rounds=new RoundMachine(state);
        var strategies=Enumerable.Range(0,4).Select(i=>(BotStrategy)((i+m)%4)).ToArray();
        var botSeeds=strategies.Select(x=>unchecked(matchSeed^(int)((uint)((int)x+1)*0x9e3779b9u))).ToArray();
        var bots=Enumerable.Range(0,4).Select(i=>new Bot(ids[i],state,rounds,BotDifficulty.Normal,botSeeds[i],strategies[i])).ToArray();
        var paid=new int[4]; var bought=new int[4]; var eligible=new int[4]; var blocked=new int[4];
        var buildSpent=new int[4]; var gain=new int[4]; var loss=new int[4]; var vp=Enumerable.Range(0,4).Select(_=>new int[s.Rounds]).ToArray();
        var coins=Enumerable.Range(0,4).Select(_=>new int[s.Rounds]).ToArray();
        var invalid=0; var budget=0; var lastRecorded=0;
        int Seat(Guid id)=>Array.IndexOf(ids,id);
        state.Events.CoinsChanged+=e=>{var d=e.Current-e.Previous; if(d>0) gain[Seat(e.Player)]+=d; else loss[Seat(e.Player)]-=d;};
        state.Events.PlacementRejected+=_=>invalid++;
        state.Events.CardPurchased+=e=>{paid[Seat(e.Player)]+=e.Price; bought[Seat(e.Player)]++; cards[e.CardId].Purchased++; cards[e.CardId].Spent+=e.Price;};
        state.Events.CardReceived+=e=>cards[e.CardId].Received++;
        state.Events.CardDiscarded+=e=>cards[e.CardId].Discarded++;
        state.Events.PlacementResolved+=e=>{cards[e.Cell.CardId].Played++; buildSpent[Seat(e.Cell.Owner)]+=s.BuildCost(CardLibrary.Get(e.Cell.CardId)); foreach(var a in e.Awards) {cards[e.Cell.CardId].PlacementVp+=a.Vp; cards[e.Cell.CardId].PlacementCoins+=a.Coins;}};
        state.Events.EventPlayed+=e=>{cards[e.CardId].Played++; cards[e.CardId].EventCoins+=e.Coins;};
        state.Events.PayoutResolved+=e=>{foreach(var a in e.Entries) if(a.Source!=null) {cards[a.Source.CardId].PayoutCoins+=a.Coins; cards[a.Source.CardId].PayoutVp+=a.Vp;}};
        state.Events.LotsRevealed+=_=>{
            foreach(var id in ids) {
                var v=rounds.GetAuctionView(id); if(v==null || !v.Eligible) continue;
                eligible[Seat(id)]++;
                var candidates=v.Lots.Where(l=>CardLibrary.Get(l.Lot.CardId).OwnershipLimit==0 || CardDraw.OwnedCount(state,id,l.Lot.CardId)<CardLibrary.Get(l.Lot.CardId).OwnershipLimit).ToArray();
                if(candidates.Any(l=>l.Lot.MinimumBid>v.AvailableCoins)) blocked[Seat(id)]++;
                foreach(var l in candidates) {cards[l.Lot.CardId].Offered++; if(l.Lot.MinimumBid>v.AvailableCoins) cards[l.Lot.CardId].Unaffordable++;}
            }
        };
        foreach(var bot in bots) bot.ChooseContract();
        if(!rounds.ContractsChosen) throw new Exception("Contracts incomplete.");
        rounds.Start(0);
        void Capture() => SimulationHarness.CaptureCompletedPayout(state,ids,vp,coins,ref lastRecorded);
        for(var step=0;step<2500 && state.Phase!=MatchPhase.EndGame;step++) {
            double now=step*0.25;
            foreach(var bot in bots) {bot.Update(now); Capture();}
            foreach(var id in ids) {
                var view=rounds.GetAuctionView(id);
                if(state.Players[id].Coins<0 || (view!=null && !view.Resolving && view.AvailableCoins+view.ReservedCoins!=state.Players[id].Coins)) budget++;
            }
            rounds.Tick(now); Capture();
        }
        invalid=Math.Max(invalid,bots.Sum(b=>b.RejectedActions));
        if(state.FinalScore==null || lastRecorded!=s.Rounds || invalid!=0 || budget!=0) throw new Exception($"Invalid match {variant.Name}/{m}.");
        foreach(var line in FinalScoring.CardEffects(state)) if(line.Source!=null) cards[line.Source.CardId].EndVp+=line.Vp;
        var seats=ids.Select((id,i)=>{
            var score=state.FinalScore.Players.Single(p=>p.Player==id);
            if(loss[i] < paid[i]+buildSpent[i]) throw new Exception("Untracked spending.");
            if(s.StartingCoins+gain[i]-loss[i]!=score.Coins) throw new Exception("Coin ledger mismatch.");
            return new { Seat=i,Strategy=strategies[i],BotSeed=botSeeds[i],score.TotalVp,FinalCoins=score.Coins,
                WinShare=state.FinalScore.Winners.Contains(id)?1.0/state.FinalScore.Winners.Count:0,
                Spent=paid[i],BuildSpent=buildSpent[i],Purchases=bought[i],EligibleMarkets=eligible[i],MarketsWithUnaffordableOption=blocked[i],
                PositiveCoinChanges=gain[i],NegativeCoinChanges=loss[i],RoundVp=vp[i],RoundCoins=coins[i],
                ScoreLines=score.Lines.Select(l=>new{l.Reason,l.Vp}).ToArray(),
                FinalCards=state.Cells().Where(c=>c.Owner==id).Select(c=>c.CardId).ToArray(),FinalHand=state.Players[id].Hand.ToArray()};
        }).ToArray();
        outcomes.Add(new { Index=m,MatchSeed=matchSeed,BoardSide=side,SeedRoadRow=row,SeedRoadColumn=column,InvalidActions=invalid,BudgetViolations=budget,Seats=seats });
        Console.Error.WriteLine($"{variant.Name}: {m+1}/{matches}");
    }
    runs.Add(new {variant.Name,variant.Settings,Matches=outcomes,Cards=cards});
}
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
File.WriteAllText(output,JsonSerializer.Serialize(new {Seed=seed,MatchesPerVariant=matches,DistinctBoardSeeds=matches/4,SourceSha256=fingerprints,Catalog=CardLibrary.All,Runs=runs},new JsonSerializerOptions {WriteIndented=true,Converters={new JsonStringEnumConverter()}}));
Console.WriteLine(output);

sealed class CardTotals {
    public int Offered {get;set;} public int Unaffordable {get;set;}
    public int Purchased {get;set;} public int Spent {get;set;} public int Received {get;set;}
    public int Played {get;set;} public int Discarded {get;set;}
    public int PlacementVp {get;set;} public int PlacementCoins {get;set;} public int EventCoins {get;set;}
    public int PayoutCoins {get;set;} public int PayoutVp {get;set;} public int EndVp {get;set;}
}
