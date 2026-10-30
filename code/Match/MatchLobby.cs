using System;
using System.Collections.Generic;
using System.Linq;

namespace CitySim.Match;

public enum LobbyPhase { Waiting, ChoosingContracts, Playing, Finished }
public sealed record LobbyMember(Guid Id,string Name,int Seat,bool Ready,bool Connected,bool Spectator);
public sealed record LobbyView(Guid Id,long Revision,Guid Host,LobbyPhase Phase,int Capacity,LobbyMember[] Members);

/// <summary>Small public ready-up state machine. Caller identities come from the authenticated transport.</summary>
public sealed class MatchLobby
{
	readonly List<LobbyMember> members=new();
	int nextSeat;
	public Guid Id { get; }
	public Guid Host { get; private set; }
	public LobbyPhase Phase { get; private set; }
	public int Capacity { get; }
	public long Revision { get; private set; }
	public event Action Changed;
	public IReadOnlyList<LobbyMember> Members => members.AsReadOnly();
	public bool CanStart => Phase==LobbyPhase.Waiting && members.Count(m=>m.Connected && !m.Spectator)>=2 && members.Where(m=>m.Connected && !m.Spectator).All(m=>m.Ready);
	public MatchLobby(Guid id,Guid host,int capacity=4)
	{
		if (id==Guid.Empty || host==Guid.Empty || capacity is < 2 or > 4) throw new ArgumentException("Invalid lobby identity or capacity.");
		Id=id; Host=host; Capacity=capacity;
	}
	void Notify() { Revision++; Changed?.Invoke(); }
	public LobbyView View() => new(Id,Revision,Host,Phase,Capacity,members.ToArray());
	internal static MatchLobby Restore(LobbyView data)
	{
		var lobby=new MatchLobby(data.Id,data.Host,data.Capacity) {Phase=data.Phase,Revision=data.Revision};
		lobby.members.AddRange(data.Members); lobby.nextSeat=data.Members.Select(m=>m.Seat).DefaultIfEmpty(-1).Max()+1; return lobby;
	}
	public bool Join(Guid player,string name)
	{
		if (player==Guid.Empty) return false;
		name=string.IsNullOrWhiteSpace(name) ? "Player" : name.Trim(); if (name.Length>40) name=name.Substring(0,40);
		var existing=members.FindIndex(m=>m.Id==player);
		if (existing>=0)
		{
			if (members[existing].Connected) return false;
			var watching=Phase==LobbyPhase.Waiting ? members.Count(m=>m.Connected && !m.Spectator)>=Capacity : members[existing].Spectator;
			members[existing]=members[existing] with {Name=name,Connected=true,Ready=false,Spectator=watching}; Notify(); return true;
		}
		var spectator=Phase!=LobbyPhase.Waiting || members.Count(m=>m.Connected && !m.Spectator)>=Capacity;
		members.Add(new(player,name,nextSeat++,false,true,spectator)); Notify(); return true;
	}
	public bool Ready(Guid caller,bool ready)
	{
		var index=members.FindIndex(m=>m.Id==caller && m.Connected && !m.Spectator);
		if (Phase!=LobbyPhase.Waiting || index<0 || members[index].Ready==ready) return false;
		members[index]=members[index] with {Ready=ready}; Notify(); return true;
	}
	public bool Start(Guid caller)
	{
		if (caller!=Host || !CanStart) return false;
		Phase=LobbyPhase.ChoosingContracts; Notify(); return true;
	}
	public bool BeginPlay()
	{
		if (Phase!=LobbyPhase.ChoosingContracts) return false;
		Phase=LobbyPhase.Playing; Notify(); return true;
	}
	public bool Finish()
	{
		if (Phase!=LobbyPhase.Playing) return false;
		Phase=LobbyPhase.Finished; Notify(); return true;
	}
	public bool Leave(Guid player)
	{
		var index=members.FindIndex(m=>m.Id==player && m.Connected); if (index<0) return false;
		members[index]=members[index] with {Connected=false,Ready=false};
		Notify(); return true;
	}
	/// <summary>The engine elects the successor; the lobby never invents a competing host.</summary>
	public bool Promote(Guid player)
	{
		var index=members.FindIndex(m=>m.Id==player && m.Connected);
		if (index<0 || Host==player) return false;
		Host=player;
		if (Phase==LobbyPhase.Waiting) members[index]=members[index] with {Spectator=false,Ready=false};
		Notify(); return true;
	}
	public bool Rematch(Guid caller)
	{
		if (caller!=Host || Phase!=LobbyPhase.Finished) return false;
		members.RemoveAll(m=>!m.Connected); Phase=LobbyPhase.Waiting;
		var seated=members.OrderBy(m=>m.Id==Host ? 0 : 1).ThenBy(m=>m.Seat).Take(Capacity).Select(m=>m.Id).ToHashSet();
		for (var i=0;i<members.Count;i++) members[i]=members[i] with {Ready=false,Spectator=!seated.Contains(members[i].Id)};
		Notify(); return true;
	}
}
