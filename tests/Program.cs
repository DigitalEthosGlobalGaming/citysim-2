if (args.Contains("--simulate"))
{
    try
    {
        int ReadInt(string option, int fallback)
        {
            var index=Array.IndexOf(args,option);
            if (index<0) return fallback;
            if (index+1>=args.Length || !int.TryParse(args[index+1],out var value)) throw new ArgumentException($"{option} requires an integer.");
            return value;
        }
        var matches=ReadInt("--matches",12); var seed=ReadInt("--seed",12345);
        var report=CitySim.Ai.SimulationHarness.Run(matches,seed,(done,total)=>Console.Error.WriteLine($"Simulated {done}/{total}"));
        if (args.Contains("--json"))
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(report,new System.Text.Json.JsonSerializerOptions { WriteIndented=true, Converters={new System.Text.Json.Serialization.JsonStringEnumConverter()} }));
        else
        {
            Console.WriteLine($"Seed {seed}; {matches} matches; four strategies, with seat assignments rotating by match.");
            foreach(var item in report.Strategies)
            {
                Console.WriteLine($"{item.Strategy}: win share {item.Wins:0.##}/{item.Games} ({item.WinRate:P1}), mean VP {item.AverageFinalVp:0.00}");
                Console.WriteLine("  Round VP:    "+string.Join(", ",item.AverageRoundVp.Select(v=>v.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture))));
                Console.WriteLine("  Round coins: "+string.Join(", ",item.AverageRoundCoins.Select(v=>v.ToString("0.00",System.Globalization.CultureInfo.InvariantCulture))));
            }
        }
        return 0;
    }
    catch(Exception e) { Console.Error.WriteLine(e.Message); return 1; }
}

try
{
	AnalyticsChecks.Run();
	foreach (var line in CitySim.Match.FoundationChecks.Run()) Console.WriteLine(line);
	Console.WriteLine("M0 foundation checks passed.");
	foreach (var line in CitySim.Match.RoundChecks.Run()) Console.WriteLine(line);
	Console.WriteLine("M1 round checks passed.");
	foreach (var line in CitySim.Match.AuctionChecks.Run()) Console.WriteLine(line);
	Console.WriteLine("M2 auction checks passed.");
	foreach (var line in CitySim.Match.CoreCardChecks.Run()) Console.WriteLine(line);
	Console.WriteLine("M3 core-card checks passed.");
	foreach (var line in CitySim.Match.ObjectivesChecks.Run()) Console.WriteLine(line);
	Console.WriteLine("M4 objective checks passed.");
	foreach (var line in CitySim.Match.NetworkChecks.Run()) Console.WriteLine(line);
	Console.WriteLine("M5 protocol checks passed (live transport checks remain separate).");
	foreach (var line in CitySim.Ai.BotChecks.Run()) Console.WriteLine(line);
	Console.WriteLine("M6 bot checks passed.");
	foreach (var line in CitySim.Match.TagEngineChecks.Run()) Console.WriteLine(line);
	Console.WriteLine("M7 tag-engine checks passed.");
	foreach (var line in CitySim.Match.TriggerChecks.Run()) Console.WriteLine(line);
	Console.WriteLine("M7 trigger checks passed.");
	foreach (var line in CitySim.Match.EconomyGrowthChecks.Run()) Console.WriteLine(line);
	Console.WriteLine("M7 economy and growth checks passed.");
	foreach (var line in CitySim.Match.CivicChecks.Run()) Console.WriteLine(line);
	Console.WriteLine("M7 Civic checks passed.");
	foreach (var line in CitySim.Match.LandmarkChecks.Run()) Console.WriteLine(line);
	Console.WriteLine("M7 landmark checks passed.");
	foreach (var line in CitySim.Match.LegendaryChecks.Run()) Console.WriteLine(line);
	Console.WriteLine("M7 Legendary checks passed.");
	foreach (var line in CitySim.Match.EventChecks.Run()) Console.WriteLine(line);
	Console.WriteLine("M7 event checks passed.");
	foreach (var line in CitySim.Match.HospitalChecks.Run()) Console.WriteLine(line);
	Console.WriteLine("M7 Hospital checks passed.");
	foreach (var line in CitySim.Match.BuilderChecks.Run()) Console.WriteLine(line);
	Console.WriteLine("M7 Builder checks passed.");
	foreach (var line in CitySim.Match.RenovationChecks.Run()) Console.WriteLine(line);
	Console.WriteLine("M7 Renovation checks passed.");
	foreach (var line in CitySim.Ai.SimulationChecks.Run()) Console.WriteLine(line);
	Console.WriteLine("M8 simulation checks passed.");
	return 0;
}
catch (Exception error)
{
	Console.Error.WriteLine(error);
	return 1;
}

