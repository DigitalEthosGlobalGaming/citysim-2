using CitySim.Cards;

namespace CitySim;

/// <summary>Presentation only: card IDs map to imported meshes, independent of match randomness.</summary>
public static class CardVisuals
{
	public record Part(string Path, float Size = 160, float X = 0, float Y = 0, float Z = 0);
	static Part C(string name, float size = 160, float x = 0, float y = 0, float z = 0) => new($"models/kenney/commercial/{name}.vmdl", size,x,y,z);
	static Part I(string name, float size = 160, float x = 0, float y = 0, float z = 0) => new($"models/kenney/industrial/{name}.vmdl", size,x,y,z);
	static Part S(string name, float size = 160, float x = 0, float y = 0, float z = 0) => new($"models/kenney/suburban/{name}.vmdl", size,x,y,z);
	static readonly Dictionary<string, Part[]> visuals = new()
	{
		["house"] = [S("building-type-a")], ["cottage"] = [S("building-type-g")],
		["apartment"] = [C("building-j")], ["shop"] = [C("building-c")],
		["cafe"] = [C("building-d",130,-20,0),C("detail-parasol-a",45,65,55)],
		["market"] = [C("building-k",140,-10,0),C("detail-awning-wide",55,62,0,35)],
		["farm"] = [], // Dedicated procedural barn and crop rows below.
		["orchard"] = [S("tree-large",65,-45,-40),S("tree-large",65,45,35),S("planter",50,40,-45)],
		["factory"] = [I("building-e")], ["park"] = [S("tree-large",80,-40,-30),S("tree-small",60,45,35),S("path-stones-long",110,20,-30)],
		["wild-garden"] = [S("planter",60,-40,-30),S("tree-small",70,40,35),S("path-stones-messy",90,10,-20)],
		["sapling"] = [S("tree-small",65)], ["forest"] = [S("tree-large",75,-45,-35),S("tree-large",75,45,30),S("tree-small",60,35,-45)],
		["vineyard"] = [S("building-type-i",125,-25,0),S("planter",45,65,35)],
		["startup"] = [C("building-b")], ["contractor"] = [I("building-p")],
		["bank"] = [C("building-i")], ["town-hall"] = [C("building-n")], ["museum"] = [C("building-e")],
		["chamber-of-commerce"] = [C("building-l")], ["warehouse"] = [I("building-j")],
		["builders-yard"] = [I("building-h",125,-25,0),I("shipping-container-a",55,65,40)],
		["hospital"] = [C("building-m")], ["tourist-office"] = [C("building-e")], ["tax-office"] = [I("building-a")],
		["depot"] = [I("building-s")], ["school"] = [C("building-k")],
		["granary"] = [I("building-k",125,-25,0),I("detail-tank-large",50,65,40)],
		["airport"] = [I("building-g")], ["skyscraper"] = [C("building-skyscraper-a")],
		["industrial-estate"] = [I("building-q")], ["housing-authority"] = [C("building-f")],
		["eco-council"] = [I("building-t",135,-15,0),I("solar-panel-flat",50,20,0,80)],
		["gardeners-guild"] = [S("building-type-i",125,-25,0),S("planter",45,65,35)],
		["architects-studio"] = [C("building-h")], ["central-station"] = [C("building-n")],
		["stadium"] = [], ["fountain"] = [], ["senate"] = [], ["monument"] = [], ["lake"] = [],
		["street"] = [], ["avenue"] = [], ["roundabout"] = [],
		["grant"] = [], ["renovation"] = [], ["ribbon-cutting"] = [], ["rush-hour"] = [], ["land-claim"] = []
	};
	static readonly HashSet<string> warned = new();
	static Model fallback;
	static Model lake;
	static Model roundaboutMarker;
	const float BuildingFootprint = 0.8f;
	static readonly Dictionary<string,Model> farmParts = new();
	static Model Fallback()
	{
		if (fallback != null) return fallback;
		var vertices = new VertexBuffer(); vertices.Init(true);
		vertices.AddCube(new Vector3(0,0,55),new Vector3(110,110,110),Rotation.Identity,Color.White);
		var mesh = new Mesh(Material.Load("materials/simple/simple_grey_50.vmat")); mesh.CreateBuffers(vertices);
		return fallback = new ModelBuilder().AddMesh(mesh).Create();
	}
	public static void Clear(GameObject root)
	{
		foreach (var child in root.Children.ToList()) { child.Parent = null; child.Destroy(); }
	}
	public static void BuildRoad(GameObject root, string path, bool roundabout = false)
	{
		Clear(root);
		var model = Model.Load(path);
		if (model == null || model.IsError || Math.Max(model.Bounds.Size.x,model.Bounds.Size.y) <= 0) { AddFallback(root,path); return; }
		var bounds = model.Bounds; var scale = 200f/Math.Max(bounds.Size.x,bounds.Size.y);
		var child = new GameObject(true,path) { Parent = root }; child.LocalRotation = Rotation.Identity; child.LocalScale = scale;
		child.LocalPosition = -new Vector3(bounds.Center.x,bounds.Center.y,bounds.Mins.z)*scale;
		child.Components.Create<ModelRenderer>().Model = model;
		if (roundabout)
		{
			var marker = new GameObject(true,"Roundabout marker") { Parent = root }; marker.LocalRotation = Rotation.Identity; marker.LocalPosition = Vector3.Up*4;
			if (roundaboutMarker == null)
			{
				var vertices = new VertexBuffer(); vertices.Init(true);
				for (var i = 0; i < 12; i++)
				{
					var angle = i * MathF.PI * 2 / 12;
					vertices.AddCube(new Vector3(MathF.Cos(angle)*25,MathF.Sin(angle)*25,3),new Vector3(15,8,6),Rotation.FromYaw(i*30+90),Color.White);
				}
				var mesh = new Mesh(Material.Load("materials/simple/simple_grey_50.vmat")); mesh.CreateBuffers(vertices);
				roundaboutMarker = new ModelBuilder().AddMesh(mesh).Create();
			}
			marker.Components.Create<ModelRenderer>().Model = roundaboutMarker;
		}
	}
	public static void Build(GameObject root, string id, bool mature = false, int visualSeed = 0)
	{
		Clear(root);
		var key = id == "sapling" && mature ? "forest" : id;
		if (key == "farm") { AddFarm(root); return; }
		if (key == "lake") { AddLake(root); return; }
		if (!visuals.TryGetValue(key, out var parts) || parts.Length == 0) { AddFallback(root,key); return; }
		var layout = Layout(parts,key,visualSeed);
		foreach (var pose in layout)
		{
			var part = pose.Part;
			var model = Model.Load(part.Path);
			if (model == null || model.IsError) { AddFallback(root,part.Path); continue; }
			var bounds = model.Bounds;
			var extent = Math.Max(bounds.Size.x,bounds.Size.y);
			if (extent <= 0) { AddFallback(root,part.Path); continue; }
			var scale = part.Size/extent * BuildingFootprint * pose.Scale;
			var child = new GameObject(true,part.Path) { Parent = root };
			child.LocalRotation = Rotation.FromYaw(pose.Yaw); child.LocalScale = scale;
			child.LocalPosition = new Vector3(pose.X,pose.Y,part.Z)*BuildingFootprint + Vector3.Up*15 - child.LocalRotation * (new Vector3(bounds.Center.x,bounds.Center.y,bounds.Mins.z)*scale);
			child.Components.Create<ModelRenderer>().Model = model;
		}
	}

	readonly record struct Pose(Part Part, float X, float Y, float Scale = 1, float Yaw = 0);
	static bool Tree(Part part) => part.Path.Contains("/tree-");
	static bool GroundPart(Part part) => part.Path.Contains("/path-stones");
	static bool Movable(Part part) => Tree(part) || part.Path.Contains("/planter.") || part.Path.Contains("/detail-parasol-") || part.Path.Contains("/shipping-container-") || part.Path.Contains("/detail-tank-");

	static Pose[] Layout(Part[] parts, string key, int seed)
	{
		var random = new Random(seed);
		float Range(float min,float max) => min+(max-min)*(float)random.NextDouble();
		// Retry the complete arrangement: props stay in their authored zones and must
		// clear each other and the tile edge. Paths intentionally run beneath foliage.
		for (var attempt=0; attempt<32; attempt++)
		{
			var poses = parts.Select(part =>
			{
				if (!Movable(part)) return new Pose(part,part.X,part.Y);
				var y = part.Y;
				// Side yards can occupy either end; facade and rooftop attachments stay fixed.
				if (!Tree(part) && key is "cafe" or "builders-yard" or "granary" or "vineyard" or "gardeners-guild") y = random.Next(2)==0 ? -45 : 45;
				return new Pose(part,part.X+Range(-9,9),y+Range(-9,9),Tree(part) ? Range(0.94f,1.06f) : 1,Tree(part) || part.Path.Contains("/detail-parasol-") ? Range(0,360) : 0);
			}).ToArray();
			var boxes = new List<(float X,float Y,float HalfX,float HalfY,bool Ground,bool Movable)>();
			var valid = true;
			foreach (var pose in poses)
			{
				var model = Model.Load(pose.Part.Path);
				if (model == null || model.IsError) continue;
				var size = model.Bounds.Size;
				var extent = Math.Max(size.x,size.y);
				if (extent <= 0) continue;
				var scale = pose.Part.Size/extent*pose.Scale;
				var angle = pose.Yaw*MathF.PI/180;
				var c = MathF.Abs(MathF.Cos(angle)); var s = MathF.Abs(MathF.Sin(angle));
				var hx = (c*size.x+s*size.y)*scale*0.5f;
				var hy = (s*size.x+c*size.y)*scale*0.5f;
				var ground = GroundPart(pose.Part); var movable = Movable(pose.Part);
				var edgeX = MathF.Abs(pose.X)+hx; var edgeY = MathF.Abs(pose.Y)+hy;
				// Free-rotating forest clusters must fit even at diagonal root angles.
				if (edgeX>112 || edgeY>112 || (key is "forest" or "sapling") && edgeX*edgeX+edgeY*edgeY>122*122) { valid=false; break; }
				if (!ground && boxes.Any(b => !b.Ground && (movable || b.Movable) && MathF.Abs(pose.X-b.X)<hx+b.HalfX+3 && MathF.Abs(pose.Y-b.Y)<hy+b.HalfY+3)) { valid=false; break; }
				boxes.Add((pose.X,pose.Y,hx,hy,ground,movable));
			}
			if (valid) return poses;
		}
		// The original composition is the safe fallback for unusually shaped assets.
		return parts.Select(part => new Pose(part,part.X,part.Y)).ToArray();
	}

	static void AddFarm(GameObject root)
	{
		void Part(string name, Action<VertexBuffer> geometry)
		{
			if (!farmParts.TryGetValue(name,out var model))
			{
				var v = new VertexBuffer(); v.Init(true); geometry(v);
				var mesh = new Mesh(Material.Load("materials/simple/simple_grey_50.vmat")); mesh.CreateBuffers(v);
				model = new ModelBuilder().AddMesh(mesh).Create(); farmParts[name] = model;
			}
			var child = new GameObject(true,name) { Parent = root }; child.LocalRotation = Rotation.Identity;
			child.LocalPosition = Vector3.Up*15; child.LocalScale = BuildingFootprint;
			child.Components.Create<ModelRenderer>().Model = model;
		}
		Part("Farm soil",v => v.AddCube(new Vector3(35,0,2),new Vector3(90,150,4),Rotation.Identity,Color.White));
		Part("Farm crops",v => {
			for(int row=0;row<5;row++) for(int col=0;col<4;col++)
				v.AddCube(new Vector3(4+col*20,-58+row*28,9),new Vector3(10,18,14),Rotation.Identity,Color.White);
		});
		Part("Farm barn",v => v.AddCube(new Vector3(-52,0,26),new Vector3(60,90,52),Rotation.Identity,Color.White));
		Part("Farm roof",v => {
			v.AddCube(new Vector3(-69,0,61),new Vector3(44,102,7),Rotation.From(new Angles(-32,0,0)),Color.White);
			v.AddCube(new Vector3(-35,0,61),new Vector3(44,102,7),Rotation.From(new Angles(32,0,0)),Color.White);
		});
		Part("Farm doors",v => v.AddCube(new Vector3(-52,-46,18),new Vector3(28,2,36),Rotation.Identity,Color.White));
	}
	static Color FarmColor(string name) => name switch {
		"Farm soil" => new Color(0.42f,0.24f,0.1f), "Farm crops" => new Color(0.4f,0.75f,0.12f),
		"Farm barn" => new Color(0.85f,0.18f,0.1f), "Farm roof" => new Color(0.25f,0.15f,0.12f), _ => Color.White };
	static void AddLake(GameObject root)
	{
		if (lake == null)
		{
			var vertices = new VertexBuffer(); vertices.Init(true);
			vertices.AddCube(new Vector3(0,0,3),new Vector3(170,170,6),Rotation.Identity,Color.White);
			var mesh = new Mesh(Material.Load("materials/simple/simple_grey_50.vmat")); mesh.CreateBuffers(vertices);
			lake = new ModelBuilder().AddMesh(mesh).Create();
		}
		var child = new GameObject(true,"Lake") { Parent = root }; child.LocalPosition = Vector3.Up*15;
		child.Components.Create<ModelRenderer>().Model = lake;
	}
	static void AddFallback(GameObject root, string key)
	{
		if (warned.Add(key)) Log.Warning($"Missing card visual: {key}");
		var child = new GameObject(true,"Missing visual") { Parent = root };
		child.LocalPosition = Vector3.Up*15;
		child.Components.Create<ModelRenderer>().Model = Fallback();
	}
	public static void Tint(GameObject root, Color tint)
	{
		foreach (var child in root.Children) { var renderer = child.Components.Get<ModelRenderer>(); if (renderer != null) renderer.Tint = child.Name.StartsWith("Farm ") ? tint * FarmColor(child.Name) : child.Name == "Missing visual" ? tint * new Color(1,0.05f,0.8f) : child.Name == "Lake" ? tint * new Color(0.2f,0.55f,1) : child.Name == "Roundabout marker" ? tint * new Color(0.5f,0.85f,0.25f) : tint; }
	}
}


