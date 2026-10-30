using System;
using System.Collections.Generic;
using System.Linq;

namespace CitySim.Match;

internal sealed record SeatAccount(ulong Account,Guid Player);

/// <summary>Host-only account-to-seat ownership. Account identity must come from the engine, never an RPC argument.</summary>
internal sealed class SeatDirectory
{
	readonly Dictionary<ulong,Guid> accounts=new();
	readonly Dictionary<Guid,Guid> connections=new();
	public Guid Player(Guid connection) => connections.GetValueOrDefault(connection);
	public Guid AccountPlayer(ulong account) => accounts.GetValueOrDefault(account);
	public Guid Bind(Guid connection,ulong authenticatedAccount)
	{
		if (connection==Guid.Empty || authenticatedAccount==0) return Guid.Empty;
		if (connections.TryGetValue(connection,out var bound) && AccountPlayer(authenticatedAccount)!=bound) return Guid.Empty;
		if (!accounts.ContainsKey(authenticatedAccount) && accounts.ContainsValue(connection)) return Guid.Empty;
		if (!accounts.TryGetValue(authenticatedAccount,out var player)) accounts[authenticatedAccount]=player=connection;
		// A new connection from the same authenticated account replaces its old session.
		foreach (var old in connections.Where(p=>p.Value==player && p.Key!=connection).Select(p=>p.Key).ToArray()) connections.Remove(old);
		connections[connection]=player; return player;
	}
	public Guid Unbind(Guid connection)
	{
		var player=Player(connection); connections.Remove(connection); return player;
	}
	public SeatAccount[] Capture() => accounts.Select(p=>new SeatAccount(p.Key,p.Value)).ToArray();
	public void Restore(IEnumerable<SeatAccount> saved)
	{
		accounts.Clear(); connections.Clear();
		foreach (var seat in saved)
		{
			if (seat.Account==0 || seat.Player==Guid.Empty || accounts.ContainsKey(seat.Account) || accounts.ContainsValue(seat.Player)) throw new ArgumentException("Invalid seat ownership checkpoint.");
			accounts.Add(seat.Account,seat.Player);
		}
	}
}
