namespace CitySim;

/// <summary>Engine geometry checks. Run TrafficChecks.Run() through the editor; the fixture is always destroyed.</summary>
public static class TrafficChecks
{
	public static IReadOnlyList<string> Run()
	{
		var results=new List<string>();
		void Check(bool condition,string message) { if(!condition) throw new Exception(message); results.Add(message); }
		var root=new GameObject(true,"Temporary traffic geometry checks") { Flags=GameObjectFlags.NotNetworked };
		try
		{
			var grid=root.Components.Create<CityGrid>(); grid.Build(7,7,generateStreets:false);
			foreach(var xy in new[]{(3,3),(2,3),(4,3),(3,2),(3,4)}) grid.Place(xy.Item1,xy.Item2,new Piece{Type=TileType.Road},animate:false,legacyRules:false);
			var centre=grid.Get(3,3);
			var directions=new[]{(1,0),(0,1),(-1,0),(0,-1)};
			foreach(var (x,y) in directions)
			{
				var incoming=new Vector3(x,y,0); var from=grid.Get(3-x,3-y);
				foreach(var sign in new[]{-1,0,1})
				{
					var outgoing=sign==0 ? incoming : new Vector3(-y*sign,x*sign,0);
					var to=grid.Get(3+(int)outgoing.x,3+(int)outgoing.y);
					var path=TrafficPath.Build(grid,new[]{from,centre,to});
					Check(path!=null && path.Length>50,$"Route {x},{y} turn {sign} has positive length");
					var samples=path.Samples.Where(s=>s.Tile==centre).ToArray();
					var entry=centre.Center-incoming*25+TrafficPath.Right(incoming)*TrafficPath.LaneOffset;
					var end=centre.Center+outgoing*25+TrafficPath.Right(outgoing)*TrafficPath.LaneOffset;
					Check(path.Samples.Any(s=>(s.Position-entry).WithZ(0).Length<0.05f && s.Forward.Dot(incoming)>0.99f),"Entry stays in right-hand lane with a matching tangent");
					Check(path.Samples.Any(s=>(s.Position-end).WithZ(0).Length<0.05f && s.Forward.Dot(outgoing)>0.99f),"Exit stays in right-hand lane with a matching tangent");
					Check(samples.All(s=>MathF.Abs(s.Position.x-centre.Center.x)<=25.1f && MathF.Abs(s.Position.y-centre.Center.y)<=25.1f),"Corner remains inside its road tile");
					Check(path.Samples.Zip(path.Samples.Skip(1),(a,b)=>a.Position.Distance(b.Position)).All(d=>d>0 && d<4),"Samples are continuous with no tile-boundary jumps");
					Check(path.At(path.Length*0.5f).Forward.Length>0.99f,"Distance lookup returns a normalized heading");
				}
			}
			Check(TrafficPath.Build(grid,new[]{centre})?.Length>10,"Shared-road building links support a short local trip");
			Check(TrafficPath.Build(grid,Array.Empty<Tile>())==null,"Empty routes are rejected");
			Check(TrafficPath.Build(grid,new[]{grid.Get(2,3),grid.Get(4,3)})==null,"Nonadjacent routes are rejected");
			var east=TrafficPath.Build(grid,new[]{grid.Get(2,3),centre,grid.Get(4,3)});
			var west=TrafficPath.Build(grid,new[]{grid.Get(4,3),centre,grid.Get(2,3)});
			var north=TrafficPath.Build(grid,new[]{grid.Get(3,2),centre,grid.Get(3,4)});
			Check(!east.ConflictsAt(west,centre),"Opposing straight lanes can pass through the same junction");
			Check(east.ConflictsAt(north,centre),"Crossing junction movements yield to one another");
			// Exercise the same current-art projection as a real board, without changing a match.
			grid.Place(2,2,new Piece{Type=TileType.House},animate:false,legacyRules:false);
			grid.Place(4,2,new Piece{Type=TileType.Business},animate:false,legacyRules:false);
			var home=grid.Get(2,2); var shop=grid.Get(4,2);
			foreach(var item in new[]{(home,"house"),(shop,"shop")})
			{
				item.Item1.BuildingRenderer.Enabled=false; item.Item1.Building.WorldRotation=Rotation.Identity;
				CardVisuals.Build(item.Item1.Building,item.Item2);
			}
			var roadTrip=TrafficPath.Build(grid,new[]{grid.Get(2,3),centre,grid.Get(4,3)});
			var doorTrip=roadTrip.WithBuildings(home,shop);
			Check(doorTrip.Origin==home && doorTrip.Destination==shop,"Journeys retain their actual origin and destination buildings");
			Check(doorTrip.DepartureEnd>10 && doorTrip.ArrivalStart<doorTrip.Length-10,"Journeys include departure and arrival driveways");
			var inside=TrafficPath.BuildingInterior(shop,new Vector3(0,-1,0));
			Check(doorTrip.At(doorTrip.Length).Position.Distance(inside)<0.01f,"Cars finish inside the destination facade");
			Check(doorTrip.At(0).Position.Distance(TrafficPath.BuildingInterior(home,new Vector3(0,-1,0)))<0.01f,"Cars emerge from their origin facade");
			Check(doorTrip.Samples.Zip(doorTrip.Samples.Skip(1),(a,b)=>a.Position.Distance(b.Position)).All(d=>d<4),"Driveway joins remain continuous");
			Check(roadTrip.Origin==null && roadTrip.Destination==null,"Per-building trips do not mutate cached road geometry");
			grid.ClearBuilding(shop.X,shop.Y);
			Check(!doorTrip.BuildingsValid,"Renovation invalidates a destination safely");
			centre.CardId="roundabout";
			foreach(var exit in new[]{grid.Get(4,3),grid.Get(3,2),grid.Get(3,4)})
			{
				var ring=TrafficPath.Build(grid,new[]{grid.Get(2,3),centre,exit});
				Check(ring.Samples.Where(s=>s.Tile==centre).All(s=>(s.Position-centre.Center).WithZ(0).Length>12.5f),"Roundabout route clears the centre island");
			}
			var retained=TrafficPath.Build(grid,new[]{grid.Get(2,3),centre,grid.Get(4,3)});
			grid.Place(1,3,new Piece{Type=TileType.Road},animate:false,legacyRules:false);
			Check(retained.IsValidFor(grid),"Adding a road preserves cars already travelling");
			grid.Clear();
			Check(!retained.IsValidFor(grid),"Rebuilt boards invalidate old journeys");
			return results;
		}
		finally { root.Destroy(); }
	}
}
