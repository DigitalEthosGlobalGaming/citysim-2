using System;
using System.Linq;
using CitySim.Cards;

namespace CitySim.Match;

/// <summary>A public emergency result. No house means no Home-tagged building existed.</summary>
public sealed record HospitalEmergencyResult(int Round, BoardCell House, BoardCell Hospital, bool Answered, int Award);

public static class HospitalEmergency
{
	/// <summary>Pick uniformly from Home-tagged cells in placement order, then choose the nearest Hospital, breaking ties by placement order.</summary>
	internal static HospitalEmergencyResult Calculate(MatchState state, Random random)
	{
		var homes = state.Cells().Where(BoardRules.IsHome).OrderBy(c => c.Order).ToArray();
		if (homes.Length == 0) return new(state.Round,null,null,false,0);
		var home = homes[random.Next(homes.Length)];
		var effect = CardLibrary.Get("hospital").Effects.Single(e => e.Rule == "hospital-emergency");
		var hospital = state.Cells().Where(c => c.CardId == "hospital" && BoardRules.Distance(home,c) <= effect.Range)
			.OrderBy(c => BoardRules.Distance(home,c)).ThenBy(c => c.Order).FirstOrDefault();
		return new(state.Round,home,hospital,hospital != null,hospital != null ? effect.Amount : 0);
	}
}
