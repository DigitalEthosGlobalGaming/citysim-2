namespace CitySim.Net;

/// <summary>
/// Host persistence on a dedicated NetworkMode.Never root. The engine includes local roots in its
/// successor-only handoff snapshot, and excludes them from ordinary join/resync snapshots.
/// Never attach this to a replicated object, or put its payload on a Sync property/RPC broadcast.
/// </summary>
public sealed class HostMatchVault : Component
{
	string payload;
	internal Func<string> Capture { get; set; }
	[Property, Hide] public string Payload
	{
		get
		{
			if (GameObject.NetworkMode != NetworkMode.Never) throw new InvalidOperationException("Private host vault must stay on its dedicated local root.");
			return Capture?.Invoke() ?? payload;
		}
		set => payload=value;
	}
}
