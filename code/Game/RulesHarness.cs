namespace CitySim;

public static class RulesHarness
{
	[ConCmd("citysim_test_network_rules")]
	public static void NetworkRules()
	{
		foreach (var line in Match.NetworkChecks.Run()) Log.Info(line);
		Log.Info("M5 protocol checks passed; live transport checks remain separate.");
	}
	[ConCmd("citysim_test_objectives")]
	public static void Objectives()
	{
		foreach (var line in Match.ObjectivesChecks.Run()) Log.Info(line);
		Log.Info("M4 objective checks passed.");
	}
	[ConCmd("citysim_test_foundations")]
	public static void Foundations()
	{
		foreach ( var line in Match.FoundationChecks.Run() ) Log.Info(line);
		Log.Info("M0 foundation checks passed.");
	}
	[ConCmd("citysim_test_rounds")]
	public static void Rounds()
	{
		foreach (var line in Match.RoundChecks.Run()) Log.Info(line);
		Log.Info("M1 round checks passed.");
	}
	[ConCmd("citysim_test_auctions")]
	public static void Auctions()
	{
		foreach (var line in Match.AuctionChecks.Run()) Log.Info(line);
		Log.Info("M2 auction checks passed.");
	}
	[ConCmd("citysim_test_cards")]
	public static void Cards()
	{
		foreach (var line in Match.CoreCardChecks.Run()) Log.Info(line);
		Log.Info("M3 core-card checks passed.");
	}
}
