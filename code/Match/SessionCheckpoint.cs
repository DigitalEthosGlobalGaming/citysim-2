using System;
using System.Linq;

namespace CitySim.Match;

internal sealed record RecipientRevision(Guid Player,long Revision);
internal sealed record SessionCheckpoint(int Version,LobbyView Lobby,HostCheckpoint Match,MatchSettings Settings,RecipientRevision[] Recipients=null,SeatAccount[] Seats=null)
{
	internal static void Validate(SessionCheckpoint saved)
	{
		if (saved?.Version!=3 || saved.Lobby==null || saved.Settings==null || saved.Seats==null)
			throw new ArgumentException("Incomplete session checkpoint.");
		var lobby=saved.Lobby;
		if (!Enum.IsDefined(lobby.Phase) || lobby.Members==null || lobby.Revision<0 || lobby.Members.Any(m=>m==null || m.Id==Guid.Empty) ||
			lobby.Members.Select(m=>m.Id).Distinct().Count()!=lobby.Members.Length)
			throw new ArgumentException("Invalid lobby checkpoint.");
		if ((lobby.Phase==LobbyPhase.Waiting)!=(saved.Match==null)) throw new ArgumentException("Lobby and match checkpoint disagree.");
		if (saved.Recipients!=null && (saved.Recipients.Any(r=>r==null || r.Player==Guid.Empty || r.Revision<0) ||
			saved.Recipients.Select(r=>r.Player).Distinct().Count()!=saved.Recipients.Length)) throw new ArgumentException("Invalid recipient revisions.");
		if (saved.Match is not { } match) return;
		if (match.Version!=2 || match.MatchId==Guid.Empty || match.Revision<0 || !double.IsFinite(match.Clock) || match.Clock<0 ||
			match.Match==null || match.Match.Settings==null || match.Rounds==null || match.Sequences==null || !double.IsFinite(match.Match.PhaseEndsAt) ||
			match.Sequences.Any(s=>s==null || s.Player==Guid.Empty || s.Sequence<0) ||
			match.Sequences.Select(s=>s.Player).Distinct().Count()!=match.Sequences.Length)
			throw new ArgumentException("Invalid host checkpoint.");
		if (lobby.Phase==LobbyPhase.ChoosingContracts && match.Match.Phase!=MatchPhase.Lobby ||
			lobby.Phase==LobbyPhase.Playing && match.Match.Phase is MatchPhase.Lobby or MatchPhase.EndGame ||
			lobby.Phase==LobbyPhase.Finished && match.Match.Phase!=MatchPhase.EndGame || !Enum.IsDefined(match.Match.Phase))
			throw new ArgumentException("Lobby and match phases disagree.");
	}
}
