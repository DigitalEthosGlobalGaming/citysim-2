namespace CitySim;

/// <summary>Distance-sampled presentation geometry. +X is forward, +Y is left: right is (dy,-dx).</summary>
public sealed class TrafficPath
{
	public const float LaneOffset = 8f;
	public readonly record struct Sample(Vector3 Position, Vector3 Forward, float Distance, Tile Tile, bool Turning);
	readonly List<Sample> samples = new();
	readonly Dictionary<Tile,Sample[]> tileSamples = new();
	public IReadOnlyList<Sample> Samples => samples;
	public float Length => samples.Count == 0 ? 0 : samples[^1].Distance;
	public int Revision { get; private set; }
	public Tile Origin { get; private set; }
	public Tile Destination { get; private set; }
	public float DepartureEnd { get; private set; }
	public float ArrivalStart { get; private set; } = float.PositiveInfinity;
	GameObject originBuilding,destinationBuilding;
	public bool BuildingsValid => (Origin==null || Origin.Building.IsValid() && Origin.Building==originBuilding)
		&& (Destination==null || Destination.Building.IsValid() && Destination.Building==destinationBuilding);
	public bool IsValidFor(CityGrid grid) => BuildingsValid && samples.All(s=>s.Tile.IsRoad && ReferenceEquals(grid.Get(s.Tile.X,s.Tile.Y),s.Tile));
	public float EntryDistance(Tile tile) => samples.First(s=>s.Tile==tile).Distance;
	public static Vector3 Right(Vector3 direction) => new(direction.y,-direction.x,0);
	Sample[] OnTile(Tile tile)
	{
		if(!tileSamples.TryGetValue(tile,out var found)) tileSamples[tile]=found=samples.Where(s=>s.Tile==tile).ToArray();
		return found;
	}
	public bool ConflictsAt(TrafficPath other,Tile tile)
	{
		foreach(var a in OnTile(tile)) foreach(var b in other.OnTile(tile))
			if((a.Position-b.Position).WithZ(0).LengthSquared<14*14) return true;
		return false;
	}

	/// <summary>Private trip copy: the shared road cache never contains a building's driveway.</summary>
	public TrafficPath WithBuildings(Tile origin,Tile destination)
	{
		if(origin==null && destination==null) return this;
		var trip=new TrafficPath { Revision=Revision,Origin=origin,Destination=destination,originBuilding=origin?.Building,destinationBuilding=destination?.Building };
		var firstIndex=origin==null ? 0 : Enumerable.Range(0,samples.Count).Where(i=>samples[i].Tile==samples[0].Tile).MinBy(i=>(samples[i].Position-samples[i].Tile.Center).WithZ(0).LengthSquared);
		var lastIndex=destination==null ? samples.Count-1 : Enumerable.Range(0,samples.Count).Where(i=>samples[i].Tile==samples[^1].Tile).MinBy(i=>(samples[i].Position-samples[i].Tile.Center).WithZ(0).LengthSquared);
		var first=samples[firstIndex]; var last=samples[lastIndex];
		if(origin?.Building.IsValid()==true)
		{
			var away=(first.Tile.Center-origin.Center).WithZ(0).Normal;
			var inside=BuildingInterior(origin,-away);
			trip.Curve(inside,inside+away*12,first.Position-first.Forward*10,first.Position,first.Tile,true);
			trip.DepartureEnd=trip.Length;
		}
		foreach(var sample in samples.Skip(firstIndex).Take(lastIndex-firstIndex+1)) trip.Add(sample.Position,sample.Forward,sample.Tile,sample.Turning);
		if(destination?.Building.IsValid()==true)
		{
			var toward=(destination.Center-last.Tile.Center).WithZ(0).Normal;
			var inside=BuildingInterior(destination,toward);
			trip.ArrivalStart=trip.Length;
			trip.Curve(last.Position,last.Position+last.Forward*10,inside-toward*12,inside,last.Tile,true);
		}
		return trip;
	}
	public static Vector3 BuildingInterior(Tile building,Vector3 toward)
	{
		// Pick the main volume (also finds the barn rather than the farm's soil or crops).
		var volumes=new[]{building.Building}.Concat(building.Building.Children).Select(c=>new {Object=c,Renderer=c.Components.Get<ModelRenderer>()})
			.Where(p=>p.Renderer?.Enabled==true && p.Renderer.Model!=null && !p.Renderer.Model.IsError)
			.Select(p=>p.Renderer.Model.Bounds.Transform(p.Object.WorldTransform)).OrderByDescending(b=>b.Size.z).ToArray();
		if(volumes.Length==0) return building.Center+Vector3.Up*(CityGrid.SurfaceHeight+0.08f);
		var bounds=volumes[0];
		var depth=(MathF.Abs(toward.x)*bounds.Size.x+MathF.Abs(toward.y)*bounds.Size.y)*0.5f;
		// Stop nine units past the facade: the car's nose enters before it is concealed.
		return (bounds.Center-toward*(depth-Math.Min(9,depth))).WithZ(building.Center.z+CityGrid.SurfaceHeight+0.08f);
	}

	public static TrafficPath Build(CityGrid grid, IReadOnlyList<Tile> route)
	{
		if (route == null || route.Count == 0 || route.Any(t => t?.IsRoad != true)) return null;
		for (var i=1;i<route.Count;i++)
			if(Math.Abs(route[i].X-route[i-1].X)+Math.Abs(route[i].Y-route[i-1].Y)!=1) return null;
		var path = new TrafficPath { Revision = grid.RoadRevision };
		for (var i=0;i<route.Count;i++)
		{
			var tile=route[i];
			var incoming=i>0 ? (tile.Center-route[i-1].Center).WithZ(0).Normal : route.Count>1 ? (route[1].Center-tile.Center).WithZ(0).Normal : Vector3.Forward;
			var outgoing=i+1<route.Count ? (route[i+1].Center-tile.Center).WithZ(0).Normal : incoming;
			var entry=i==0 ? EdgeNormal(grid,tile,-incoming) : Vector3.Zero;
			var exit=i==route.Count-1 ? EdgeNormal(grid,tile,outgoing) : Vector3.Zero;
			if (entry.LengthSquared>0) incoming=-entry;
			if (exit.LengthSquared>0) outgoing=exit;
			if (incoming.Dot(outgoing)<-0.5f) return null;
			var centre=tile.Center+Vector3.Up*RoadHeight(tile);
			var start=centre-incoming*(i==0 && entry.LengthSquared==0 ? 10f : 25f)+Right(incoming)*LaneOffset;
			var end=centre+outgoing*(i==route.Count-1 && exit.LengthSquared==0 ? 10f : 25f)+Right(outgoing)*LaneOffset;
			if (tile.CardId=="roundabout" && route.Count>1)
			{
				// Right-hand traffic circulates counterclockwise, clear of the raised island.
				var a=MathF.Atan2(incoming.y,incoming.x)+MathF.PI*7/6;
				var b=MathF.Atan2(outgoing.y,outgoing.x)-MathF.PI/6;
				while (b<=a) b+=MathF.PI*2;
				Vector3 Ring(float angle) => centre+new Vector3(MathF.Cos(angle),MathF.Sin(angle),0)*13f;
				Vector3 Tangent(float angle) => new(-MathF.Sin(angle),MathF.Cos(angle),0);
				path.Curve(start,start+incoming*7,Ring(a)-Tangent(a)*5,Ring(a),tile,true);
				var steps=(int)MathF.Ceiling((b-a)*10);
				for (var step=1;step<=steps;step++) { var angle=a+(b-a)*step/steps; path.Add(Ring(angle),Tangent(angle),tile,true); }
				path.Curve(Ring(b),Ring(b)+Tangent(b)*5,end-outgoing*7,end,tile,true);
			}
			else if (incoming.Dot(outgoing)<0.5f)
			{
				var control=centre+(Right(incoming)+Right(outgoing))*LaneOffset;
				const float k=0.55228475f;
				path.Curve(start,start+(control-start)*k,end+(control-end)*k,end,tile,true);
			}
			else path.Curve(start,Vector3.Lerp(start,end,1f/3),Vector3.Lerp(start,end,2f/3),end,tile,false);
		}
		return path.Length>1 ? path : null;
	}
	public static float RoadHeight(Tile tile)
	{
		// Roads replace the ground slab; use the actual mesh top, not the ground height.
		var road=tile.Building?.Children.FirstOrDefault(c=>c.Components.Get<ModelRenderer>()?.Model != null);
		var renderer=road?.Components.Get<ModelRenderer>();
		return renderer?.Model is { } model ? road.LocalPosition.z*tile.Building.WorldScale.z+model.Bounds.Maxs.z*road.WorldScale.z+0.08f : 1.08f;
	}
	static Vector3 EdgeNormal(CityGrid grid,Tile tile,Vector3 preferred)
	{
		var candidates=new List<Vector3>();
		if(tile.X==0) candidates.Add(-Vector3.Forward);
		if(tile.X==grid.Width-1) candidates.Add(Vector3.Forward);
		if(tile.Y==0) candidates.Add(-Vector3.Left);
		if(tile.Y==grid.Height-1) candidates.Add(Vector3.Left);
		return candidates.OrderByDescending(d=>d.Dot(preferred)).FirstOrDefault();
	}
	void Curve(Vector3 a,Vector3 b,Vector3 c,Vector3 d,Tile tile,bool turning)
	{
		for(var i=0;i<=16;i++)
		{
			var t=i/16f; var u=1-t;
			Add(a*(u*u*u)+b*(3*u*u*t)+c*(3*u*t*t)+d*(t*t*t),((b-a)*(u*u)+(c-b)*(2*u*t)+(d-c)*(t*t)).Normal,tile,turning);
		}
	}
	void Add(Vector3 position,Vector3 forward,Tile tile,bool turning)
	{
		var distance=samples.Count==0 ? 0 : samples[^1].Distance+samples[^1].Position.Distance(position);
		if(samples.Count>0 && distance-samples[^1].Distance<0.001f) return;
		samples.Add(new(position,forward,distance,tile,turning));
	}
	public Sample At(float distance)
	{
		if(distance<=0) return samples[0];
		if(distance>=Length) return samples[^1];
		var lo=0; var hi=samples.Count-1;
		while(hi-lo>1) { var mid=(lo+hi)/2; if(samples[mid].Distance<distance) lo=mid; else hi=mid; }
		var a=samples[lo]; var b=samples[hi]; var t=(distance-a.Distance)/(b.Distance-a.Distance);
		return new(Vector3.Lerp(a.Position,b.Position,t),Vector3.Lerp(a.Forward,b.Forward,t).Normal,distance,b.Tile,a.Turning || b.Turning);
	}
}
