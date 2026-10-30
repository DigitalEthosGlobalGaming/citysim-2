namespace CitySim;

public enum VehicleKind { Sedan, Hatchback, Suv, Taxi, Delivery, Truck, Ambulance, Van, Tractor, GarbageTruck, Firetruck }

/// <summary>Local visual actor only. Never calls the match, pays coins or blocks player input.</summary>
public sealed class Vehicle : Component
{
	public static readonly List<Vehicle> All = new();
	static readonly string[] models={"sedan","hatchback-sports","suv","taxi","delivery","truck","ambulance","van","tractor","garbage-truck","firetruck"};
	static readonly Dictionary<VehicleKind,Model> loaded=new();
	static readonly HashSet<VehicleKind> warned=new();
	public VehicleKind Kind { get; private set; }
	public bool IsEmergency => Kind==VehicleKind.Ambulance;
	public float Speed { get; private set; }
	public float CruiseSpeed => cruise*1.5f;
	public float LaneDeviation { get; private set; }
	public float DistanceTravelled { get; private set; }
	public TrafficPath Path { get; private set; }
	public Vector3 Heading { get; private set; }
	public TrafficController Owner { get; private set; }
	public Tile CurrentTile => Path?.At(DistanceTravelled).Tile;
	GameObject visual;
	ModelRenderer beacon;
	SoundHandle siren;
	float cruise,age,stallAge,retireAge=-1,retireScale=1;
	float laneBias,lanePhase,driftRate;
	int checkedRevision;

	public static Vehicle Spawn(TrafficController owner,TrafficPath path,VehicleKind kind)
	{
		if(path==null || All.Count>=CityGame.MaxVehicles) return null;
		var go=new GameObject(true,$"Traffic · {kind}") { Parent=owner.GameObject,Flags=GameObjectFlags.NotNetworked };
		var car=go.Components.Create<Vehicle>();
		car.Owner=owner; car.Path=path; car.Kind=kind;
		car.checkedRevision=path.Revision;
		car.cruise=(kind is VehicleKind.Truck or VehicleKind.Delivery or VehicleKind.GarbageTruck ? 23f : kind==VehicleKind.Tractor ? 17f : car.IsEmergency ? 34f : 28f)*owner.Random.Float(0.82f,1.18f);
		car.laneBias=owner.Random.Float(-0.6f,0.6f); car.lanePhase=owner.Random.Float(0,MathF.PI*2); car.driftRate=owner.Random.Float(0.3f,0.6f);
		car.BuildVisual();
		var start=path.At(0); go.WorldPosition=start.Position; car.Heading=start.Forward;
		go.WorldRotation=Rotation.LookAt(start.Forward,Vector3.Up);
		All.Add(car);
		return car;
	}
	void BuildVisual()
	{
		if(!loaded.TryGetValue(Kind,out var model)) loaded[Kind]=model=Model.Load($"models/kenney/cars/{models[(int)Kind]}.vmdl");
		visual=new GameObject(true,"Body") { Parent=GameObject,Flags=GameObjectFlags.NotNetworked };
		// These FBX models face +X, matching the actor's sampled lane tangent.
		visual.LocalRotation=Rotation.Identity;
		var renderer=visual.Components.Create<ModelRenderer>();
		if(model==null || model.IsError || model.Bounds.Size.x<=0)
		{
			if(warned.Add(Kind)) Log.Warning($"Traffic model unavailable: {Kind}; using a visible fallback.");
			renderer.Model=Model.Load("models/dev/box.vmdl"); visual.LocalScale=new Vector3(0.12f,0.07f,0.05f);
			renderer.Tint=new Color(0.9f,0.65f,0.2f); visual.LocalPosition=Vector3.Up*2;
		}
		else
		{
			var length=Kind is VehicleKind.Truck or VehicleKind.Delivery or VehicleKind.Ambulance or VehicleKind.GarbageTruck or VehicleKind.Firetruck ? 15f : 12.5f;
			var scale=length/model.Bounds.Size.x;
			renderer.Model=model; visual.LocalScale=scale;
			visual.LocalPosition=-new Vector3(model.Bounds.Center.x,model.Bounds.Center.y,model.Bounds.Mins.z)*scale;
		}
		if(IsEmergency)
		{
			var light=new GameObject(true,"Response beacon") { Parent=GameObject,Flags=GameObjectFlags.NotNetworked };
			light.LocalPosition=new Vector3(1,0,6.7f); light.LocalScale=new Vector3(0.022f,0.055f,0.012f);
			beacon=light.Components.Create<ModelRenderer>(); beacon.Model=Model.Load("models/dev/box.vmdl");
		}
	}
	protected override void OnStart()
	{
		if(IsEmergency)
		{
			siren=Sound.Play("sounds/vehicles/ambulance/ambulance.siren.sound",WorldPosition);
			if(siren!=null) siren.Volume=PlayerPreferences.EffectsVolume*0.12f;
		}
	}
	public void Retire() { if(retireAge<0) { retireScale=WorldScale.x; retireAge=0; } }
	protected override void OnUpdate()
	{
		if(Path==null || !Owner.IsValid()) { GameObject.Destroy(); return; }
		if(!Owner.Running) return;
		var dt=Math.Min(Time.Delta,0.1f); age+=dt;
		if(!Path.BuildingsValid) Retire();
		if(checkedRevision!=Owner.Grid.RoadRevision)
		{
			checkedRevision=Owner.Grid.RoadRevision;
			if(!Path.IsValidFor(Owner.Grid)) Retire();
		}
		if(retireAge>=0)
		{
			retireAge+=dt; GameObject.WorldScale=Math.Max(0.001f,retireScale*(1-Smooth(retireAge/0.35f)));
			if(retireAge>=0.35f) GameObject.Destroy();
			return;
		}
		var here=Path.At(DistanceTravelled); var ahead=Path.At(DistanceTravelled+23);
		var target=(here.Turning || ahead.Turning ? CruiseSpeed*0.62f : CruiseSpeed)*(1+0.025f*MathF.Sin(age*0.4f+lanePhase));
		if(DistanceTravelled<Path.DepartureEnd || DistanceTravelled>Path.ArrivalStart-15) target=Math.Min(target,12.5f);
		var stopAt=Path.Length;
		if(!Owner.CanEnter(this,ahead.Tile))
		{
			stopAt=Math.Max(DistanceTravelled,Path.EntryDistance(ahead.Tile)-9);
			target=Math.Min(target,Math.Max(0,stopAt-DistanceTravelled)*1.8f);
		}
		foreach(var other in All)
		{
			if(other==this || !other.IsValid() || other.Owner!=Owner) continue;
			var delta=(other.WorldPosition-WorldPosition).WithZ(0);
			var forward=delta.Dot(Heading); var lateral=MathF.Abs(delta.Dot(TrafficPath.Right(Heading)));
			if(forward>0 && forward<45 && lateral<7 && other.Heading.Dot(Heading)>0.65f)
				target=Math.Min(target,Math.Max(0,other.Speed+(forward-19)*(forward<19 ? 3f : 1.4f)));
			if(!IsEmergency && other.IsEmergency && delta.Length<50) target=Math.Min(target,CruiseSpeed*0.45f);
		}
		var acceleration=target<Speed ? 90f : 30f;
		Speed+=Math.Clamp(target-Speed,-acceleration*dt,acceleration*dt);
		DistanceTravelled=Math.Min(stopAt,DistanceTravelled+Speed*dt);
		here=Path.At(DistanceTravelled); Heading=here.Forward;
		// A stable driver preference plus very slow, tiny drift; always well inside the right lane.
		var roadWeight=Smooth((DistanceTravelled-Path.DepartureEnd)/12f)*Smooth((Path.ArrivalStart-DistanceTravelled)/12f);
		LaneDeviation=(laneBias+MathF.Sin(age*driftRate+lanePhase)*0.15f)*roadWeight;
		WorldPosition=here.Position+TrafficPath.Right(Heading)*LaneDeviation; WorldRotation=Rotation.LookAt(Heading,Vector3.Up);
		Owner.ReleaseBehind(this,here.Tile);
		var departure=Path.Origin==null ? Smooth(age/0.45f) : Smooth(DistanceTravelled/5f);
		var scale=Math.Min(departure,Smooth((Path.Length-DistanceTravelled)/(Path.Destination==null ? 9f : 5f)));
		GameObject.WorldScale=Math.Max(0.001f,scale);
		// Subtle body lean sells steering without moving the lane or camera.
		var turn=Heading.Cross(ahead.Forward).z;
		visual.LocalRotation=Rotation.Slerp(visual.LocalRotation,Rotation.From(new Angles(0,0,-turn*2.5f)),1-MathF.Exp(-dt*7));
		if(beacon.IsValid()) beacon.Tint=PlayerPreferences.Current.ReduceFlashing ? new Color(0.45f,0.65f,1) : Color.Lerp(new Color(0.3f,0.5f,1),new Color(1,0.25f,0.15f),(MathF.Sin(age*8)+1)*0.5f);
		if(siren!=null) { siren.Position=WorldPosition; siren.Volume=PlayerPreferences.EffectsVolume*0.12f*scale; }
		stallAge=Speed<0.5f ? stallAge+dt : 0;
		if(stallAge>3) Owner.ReleaseAhead(this);
		// Waiting never makes a valid car vanish in the street.
		if(DistanceTravelled>=Path.Length) Retire();
	}
	static float Smooth(float t) { t=Math.Clamp(t,0,1); return t*t*(3-2*t); }
	protected override void OnDestroy()
	{
		All.Remove(this); siren?.Stop(0.25f);
		if(Owner.IsValid()) Owner.Release(this);
	}
}
