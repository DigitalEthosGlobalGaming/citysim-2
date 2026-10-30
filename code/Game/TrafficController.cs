using CitySim.Match;

namespace CitySim;

/// <summary>A client-local living-city layer. Reads public buildings/events; never mutates match state.</summary>
public sealed class TrafficController : Component
{
	// A separate RNG is essential: cosmetic departures must not advance gameplay randomness.
	public Random Random { get; } = new();
	public CityGrid Grid => game?.Grid;
	public bool Running => game.IsValid() && Grid?.IsBuilt==true && game.Screens.Top is not (GameScreen.Pause or GameScreen.Settings or GameScreen.Recovery);
	public int TargetPopulation { get; private set; }
	public int SuccessfulSpawns { get; private set; }
	public int UnroutableTrips { get; private set; }
	public int CachedRoutes => routes.Count;
	CityGame game;
	MatchState match;
	int roadRevision=-1;
	float untilSpawn,burstUntil,age;
	readonly List<Tile> roads=new(),edges=new();
	readonly Dictionary<(Tile,Tile),TrafficPath> routes=new();
	readonly Dictionary<Tile,List<Vehicle>> junctions=new();
	sealed record Trip(Tile From,Tile To,VehicleKind Kind,Tile Origin,Tile Destination,int Attempts);
	readonly Queue<Trip> requests=new();
	public void Bind(CityGame city)
	{
		if(game!=null) game.MatchChanged-=MatchChanged;
		game=city; game.MatchChanged+=MatchChanged; MatchChanged();
	}
	void MatchChanged()
	{
		if(match!=null) { match.Events.PayoutResolved-=Payout; match.Events.HospitalEmergency-=Emergency; match.Events.EventPlayed-=EventPlayed; match.Events.PhaseChanged-=Phase; }
		Clear(); match=game.Match;
		if(match!=null) { match.Events.PayoutResolved+=Payout; match.Events.HospitalEmergency+=Emergency; match.Events.EventPlayed+=EventPlayed; match.Events.PhaseChanged+=Phase; }
	}
	public void Clear()
	{
		foreach(var car in Vehicle.All.Where(c=>c.Owner==this).ToArray()) if(car.IsValid()) car.GameObject.Destroy();
		requests.Clear(); junctions.Clear(); routes.Clear(); roads.Clear(); edges.Clear();
		roadRevision=-1; untilSpawn=0.25f; burstUntil=0; TargetPopulation=0;
	}
	void RefreshRoads()
	{
		if(Grid==null || roadRevision==Grid.RoadRevision) return;
		roadRevision=Grid.RoadRevision; routes.Clear(); roads.Clear(); edges.Clear();
		roads.AddRange(Grid.All().Where(t=>t.IsRoad)); edges.AddRange(Grid.EdgeRoads());
	}
	TrafficPath Route(Tile from,Tile to)
	{
		RefreshRoads();
		if(from?.IsRoad!=true || to?.IsRoad!=true) return null;
		var key=(from,to);
		if(routes.TryGetValue(key,out var cached)) return cached;
		if(routes.Count>=256) routes.Clear();
		var path=Grid.FindRoadPath(from,to);
		return routes[key]=TrafficPath.Build(Grid,path);
	}
	Tile Pick(IReadOnlyList<Tile> choices) => choices.Count==0 ? null : choices[Random.Next(choices.Count)];
	Tile RoadAt(BoardCell cell)
	{
		var tile=cell==null ? null : Grid.Get(cell.X,cell.Y);
		return tile==null ? null : Pick(Grid.AdjacentRoads(tile).ToArray());
	}
	void QueueTrip(Tile from,Tile to,VehicleKind kind,Tile origin=null,Tile destination=null,int attempts=0)
	{
		if(from==null || to==null || requests.Count>=32) return;
		if(kind==VehicleKind.Ambulance)
		{
			var pending=requests.ToArray(); requests.Clear(); requests.Enqueue(new(from,to,kind,origin,destination,attempts));
			foreach(var item in pending) requests.Enqueue(item);
		}
		else requests.Enqueue(new(from,to,kind,origin,destination,attempts));
	}
	(Tile From,Tile To) RouteBetweenBuildings(BoardCell a,BoardCell b)
	{
		var from=a==null ? null : Grid.Get(a.X,a.Y); var to=b==null ? null : Grid.Get(b.X,b.Y);
		if(from==null || to==null) return default;
		foreach(var start in Grid.AdjacentRoads(from)) foreach(var end in Grid.AdjacentRoads(to))
			if(Route(start,end)!=null) return (start,end);
		return default;
	}
	void Payout(PayoutResult result)
	{
		burstUntil=age+7;
		RefreshRoads();
		// Cosmetic arrivals illustrate busy buildings without economic pairings.
		foreach(var cell in result.Entries.Where(e=>e.Coins>0 && e.Source!=null).Select(e=>e.Source).Distinct().OrderBy(_=>Random.Next()).Take(12))
			QueueTrip(Pick(edges),RoadAt(cell),cell.CardId is "farm" or "orchard" or "factory" ? VehicleKind.Delivery : VehicleKind.Sedan,destination:Grid.Get(cell.X,cell.Y));
	}
	void EventPlayed(CardEventResult result) { if(result.CardId=="rush-hour") burstUntil=age+12; }
	void Phase(PhaseChange phase)
	{
		if(phase.Phase!=MatchPhase.Build || match==null) return;
		RefreshRoads();
		foreach(var cell in match.Cells().Where(c=>c.CardId is "farm" or "shop" or "warehouse").OrderBy(_=>Random.Next()).Take(3))
			QueueTrip(Pick(edges),RoadAt(cell),VehicleKind.Delivery,destination:Grid.Get(cell.X,cell.Y));
	}
	void Emergency(HospitalEmergencyResult result)
	{
		if(result?.House==null || Vehicle.All.Any(v=>v.Owner==this && v.IsEmergency)) return;
		RefreshRoads();
		var pair=RouteBetweenBuildings(result.Hospital,result.House);
		var end=pair.To ?? RoadAt(result.House); if(end==null) return;
		// The public result already decided the reward. A missing road never changes it.
		var start=pair.From;
		if(start==null || Route(start,end)==null) start=edges.OrderBy(t=>t.Center.Distance(end.Center)).FirstOrDefault(t=>Route(t,end)!=null);
		if(start!=null) QueueTrip(start,end,VehicleKind.Ambulance,pair.From==null ? null : Grid.Get(result.Hospital.X,result.Hospital.Y),Grid.Get(result.House.X,result.House.Y));
	}
	protected override void OnUpdate()
	{
		if(!game.IsValid() || !Running) return;
		if(!ReferenceEquals(match,game.Match)) MatchChanged();
		RefreshRoads(); age+=Time.Delta; untilSpawn-=Time.Delta;
		if(untilSpawn>0) return;
		var buildings=Grid.All().Where(t=>!t.IsRoad && t.Type!=TileType.Empty).ToArray();
		TargetPopulation=roads.Count<2 ? 0 : Math.Clamp(3+buildings.Length/4+roads.Count/20,3,18)+(age<burstUntil ? 4 : 0);
		untilSpawn=Random.Float(0.65f,1.3f);
		if(Vehicle.All.Count>=CityGame.MaxVehicles) return;
		var emergencyWaiting=requests.TryPeek(out var next) && next.Kind==VehicleKind.Ambulance;
		// An emergency has a reserved presentation slot, even when ambient traffic is full.
		if(!emergencyWaiting && Vehicle.All.Count(v=>v.Owner==this)>=TargetPopulation) return;
		if(requests.TryDequeue(out var request))
		{
			if(!TrySpawn(request.From,request.To,request.Kind,request.Origin,request.Destination) && request.Attempts<6 && Route(request.From,request.To)!=null)
				QueueTrip(request.From,request.To,request.Kind,request.Origin,request.Destination,request.Attempts+1);
			return;
		}
		// Through traffic establishes life even on the first streets and in sandbox.
		var destinations=buildings.Where(t=>t.Type is TileType.House or TileType.Business && Grid.AdjacentRoads(t).Any()).ToArray();
		for(var attempt=0;attempt<6;attempt++)
		{
			var from=Pick(edges.Count>0 ? edges : roads);
			Tile to=null,origin=null,destination=null;
			if(destinations.Length>0 && Random.Next(3)==0) { origin=Pick(destinations); from=Pick(Grid.AdjacentRoads(origin).ToArray()); }
			if(destinations.Length>0 && Random.Next(3)==0) { destination=Pick(destinations); to=Pick(Grid.AdjacentRoads(destination).ToArray()); }
			to ??= Pick(edges.Count>1 ? edges : roads);
			if(origin==destination && origin!=null || from==to && origin==null && destination==null) continue;
			var kind=Random.Next(12) switch {0=>VehicleKind.Taxi,1=>VehicleKind.Van,2=>VehicleKind.Delivery,3=>VehicleKind.Suv,4=>VehicleKind.Hatchback,_=>VehicleKind.Sedan};
			if(destination?.CardId is "farm" or "warehouse" or "factory" || origin?.CardId is "farm" or "warehouse" or "factory") kind=Random.Next(3)==0 ? VehicleKind.Truck : VehicleKind.Delivery;
			if((origin?.CardId=="farm" || destination?.CardId=="farm") && Random.Next(5)==0) kind=VehicleKind.Tractor;
			if(TrySpawn(from,to,kind,origin,destination)) break;
		}
	}
	bool TrySpawn(Tile from,Tile to,VehicleKind kind,Tile origin=null,Tile destination=null)
	{
		var path=Route(from,to);
		if(path==null) { UnroutableTrips++; return false; }
		path=path.WithBuildings(origin,destination);
		var first=path.At(0);
		if(IsJunction(first.Tile) && junctions.TryGetValue(first.Tile,out var occupants) && occupants.Any(v=>v.IsValid() && path.ConflictsAt(v.Path,first.Tile))) return false;
		if(Vehicle.All.Any(v=>v.IsValid() && v.Owner==this && v.WorldPosition.Distance(first.Position)<23)) return false;
		var car=Vehicle.Spawn(this,path,kind); if(car==null) return false;
		CanEnter(car,first.Tile);
		SuccessfulSpawns++; return true;
	}
	bool IsJunction(Tile tile) => tile!=null && (tile.CardId=="roundabout" || Enumerable.Range(0,4).Count(d=>Grid.Neighbour(tile,d)?.IsRoad==true)>=3);
	public bool CanEnter(Vehicle car,Tile tile)
	{
		if(!IsJunction(tile)) return true;
		if(!junctions.TryGetValue(tile,out var occupants)) junctions[tile]=occupants=new();
		occupants.RemoveAll(v=>!v.IsValid());
		if(occupants.Any(v=>v!=car && car.Path.ConflictsAt(v.Path,tile))) return false;
		if(!occupants.Contains(car)) occupants.Add(car);
		return true;
	}
	public void ReleaseBehind(Vehicle car,Tile current)
	{
		var front=car.Path.At(car.DistanceTravelled+23).Tile;
		var rear=car.Path.At(Math.Max(0,car.DistanceTravelled-9)).Tile;
		foreach(var pair in junctions.Where(p=>p.Key!=current && p.Key!=front && p.Key!=rear).ToArray()) pair.Value.Remove(car);
	}
	public void ReleaseAhead(Vehicle car)
	{
		var rear=car.Path.At(Math.Max(0,car.DistanceTravelled-9)).Tile;
		foreach(var pair in junctions.Where(p=>p.Key!=car.CurrentTile && p.Key!=rear).ToArray()) pair.Value.Remove(car);
	}
	public void Release(Vehicle car) { foreach(var occupants in junctions.Values) occupants.Remove(car); }
	protected override void OnDestroy()
	{
		if(game!=null) game.MatchChanged-=MatchChanged;
		if(match!=null) { match.Events.PayoutResolved-=Payout; match.Events.HospitalEmergency-=Emergency; match.Events.EventPlayed-=EventPlayed; match.Events.PhaseChanged-=Phase; }
		Clear();
	}
}
