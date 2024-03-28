using Degg.Util;
using Sandbox.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Degg.GridSystem
{



	// Grid map is an instance of a space. This is the top level "grid" itself.
	[Category("Degg.GridSystem")]
	public partial class GridMap : Component, Component.ExecuteInEditor
	{
		public List<GridSpace> Grid { get; set; }

		[Property]
		public int XSize { get; set; }

		[Property]
		public int YSize { get; set; }

		[Property]
		public int AmountToCreateAtOnce { get; set; }

		[Property]
		public GameObject GridSpaceTemplate { get; set; }

		public int CurrentX { get; set; }
		public int CurrentY { get; set; }

		public bool IsStarting { get; set; }

		public Queue<Func<bool>> SetupFunctions { get; set; }


		[Property]
		public Vector2 GridSize { get; set; }

		
		public bool IsSetup { get; set; }

		public GridMap()
		{

		}

		protected override void OnAwake()
		{
			base.OnAwake();
		}

		protected override void OnDisabled()
		{
			base.OnDisabled();
			Cleanup();

		}
		protected override void OnDestroy()
		{
			base.OnDestroy();

		}

		public void Cleanup()
		{

			var items = GetGridAsList();
			AdvLog.Info( "Item clean up" );
			foreach ( var item in items )
			{
				AdvLog.Info( "Item clean up" );
				item.Cleanup();
			}
			SetupFunctions = new Queue<Func<bool>>();

			this.IsSetup = false;
		}
		protected override void DrawGizmos()
		{
			base.DrawGizmos();
		}
		protected override void OnUpdate()
		{
			base.OnUpdate();
			ServerTick();
		}
		protected override void OnStart()
		{
			base.OnStart();
			AdvLog.Info( "Start" );
		}

		protected override void OnValidate()
		{
			base.OnValidate();
			if ( this.Enabled )
			{
				this.Init();
			} else
			{
				this.Cleanup();
			}
		}




		public Vector3 GetWorldSpace( int x, int y )
		{
			var percentages = (new Vector2( x, y )) * GridSize;
			return new Vector3( percentages.x, percentages.y, 0 ) + GameObject.Transform.Position - (GetMapSize() / 2);
		}

		public void Init()
		{
			if ( this.Enabled )
			{
				Init( GameObject.Transform.Position, GridSize, XSize, YSize );
			}
		}

		public void Init( Vector3 position, Vector2 gridSize, int xSize, int ySize )
		{
			if ( !IsSetup )
			{
				XSize = xSize;
				YSize = ySize;
				Grid = new List<GridSpace>( xSize * ySize );
				Transform.Position = position;
				GridSize = gridSize;
				SetupFunctions = new Queue<Func<bool>>();
				for ( int x = 0; x < XSize; x++ )
				{
					for ( int y = 0; y < YSize; y++ )
					{
						var tempX = x;
						var tempY = y;
						SetupFunctions.Enqueue( () => CreateNextTile( tempX, tempY ) );
					}
				}
			}
		}


		public bool CreateNextTile( int x, int y )
		{
			if (!Enabled)
			{
				return false;
			}
			try
			{
				var templateInstance = GridSpaceTemplate.Clone();
				templateInstance.BreakFromPrefab();
				templateInstance.Name = $"GridSpace_{x}_{y}";
				var newSpace = templateInstance.Components.Get<GridSpace>();
				newSpace.Transform.Scale = Transform.Scale;
				newSpace.Map = this;
				newSpace.GridPosition = new Vector2( x, y );
				Grid.Add( newSpace );
				newSpace.SetParent( this );
				OnSpaceSetup( newSpace );
				newSpace.OnAddToMap();
				return true;
			}
			catch ( Exception e )
			{
				AdvLog.Info( e );
				return false;
			}

		}

		public void CheckFinish()
		{
			IsStarting = true;
		}

		public int TransformGridPosition( int x, int y )
		{
			return (x * YSize) + y;
		}
		public int TransformGridPosition( float x, float y )
		{
			return ((int)x * XSize) + ((int)y);
		}

		public List<GridSpace> GetGridAsList()
		{
			var grid = new List<GridSpace>();

			foreach ( var item in Grid )
			{
				grid.Add( item );
			}
			return grid;
		}


		public GridSpace GetSpace( Vector2 position )
		{
			return GetSpace( position.x, position.y );
		}

		public Vector2 GetGridSize()
		{
			return this.GridSize;
		}
		public Vector3 GetMapSize()
		{
			return new Vector3( this.GridSize * new Vector2( XSize, YSize ) );
		}

		public GridSpace GetSpace( float x, float y )
		{
			return GetSpace( (int)x, (int)y );
		}

		public GridSpace GetSpace( int x, int y )
		{
			if ( Grid == null )
			{
				return null;
			}
			if ( (x < XSize && x >= 0) && (y < YSize && y >= 0) )
			{
				var amount = TransformGridPosition( x, y );
				if ( amount >= 0 && amount < Grid.Count )
				{
					return Grid[amount];
				}

			}

			return null;
		}


		public List<GridSpace> CreatePath( Vector2 start, Vector2 end )
		{
			var mesh = new NavMesh( this );
			return mesh.BuildPath( start, end );
		}
		public List<GridSpace> CreatePath( GridSpace start, GridSpace end )
		{
			return CreatePath( start.GridPosition, end.GridPosition );
		}

		public bool IsPath( Vector2 start, Vector2 end )
		{
			var mesh = new NavMesh( this );
			return mesh.BuildPath( start, end ).Count > 0;
		}
		public GridSpace GetRandomSpace()
		{
			var rnd = new Random();
			var x = rnd.Next( 0, XSize - 1 );
			var y = rnd.Next( 0, YSize - 1 );

			return GetSpace( (int)x, (int)y );
		}

		public List<T> GetTilesAtEdgeOfMap<T>() where T : GridSpace
		{
			List<T> tiles = new();
			for ( int i = 0; i < XSize - 1; i++ )
			{
				tiles.Add( (T)GetSpace( i, 0 ) );
				tiles.Add( (T)GetSpace( i, YSize - 1 ) );
			}
			for ( int i = 0; i < YSize - 1; i++ )
			{
				tiles.Add( (T)GetSpace( 0, i ) );
				tiles.Add( (T)GetSpace( XSize - 1, i ) );
			}

			return tiles;
		}

		public bool MoveItem( GridItem item, Vector2 newPosition )
		{
			var oldSpace = item.Space;
			var newSpace = GetSpace( newPosition );
			if ( newSpace == null )
			{
				return false;
			}
			oldSpace.RemoveItem( item, false );
			newSpace.AddItem( item, false );

			item.OnMove( newPosition, oldSpace.Transform.Position );

			return true;
		}

		public bool AddItem( GridItem item, Vector2 newPosition )
		{
			if ( item.Space != null )
			{
				return MoveItem( item, newPosition );
			}

			var newSpace = GetSpace( newPosition );
			if ( newSpace == null )
			{
				return false;
			}

			newSpace.AddItem( item );
			return true;
		}

		public virtual void OnSpaceSetup( GridSpace space )
		{

		}

		public virtual void OnSetup()
		{

		}




		public virtual void ClientTick()
		{
			if ( IsSetup )
			{
				foreach ( var item in Grid )
				{
					if ( item != null )
					{
						item.ClientTick( Time.Delta, Time.Now );
					}
				}
			}
		}


		public virtual void ServerTick()
		{
			var startTime = Time.Now;
			var amountToCreateAtOnce = AmountToCreateAtOnce;
			if ( amountToCreateAtOnce  < 0)
			{
				amountToCreateAtOnce = 1;
			} 

			if ( IsSetup == false && Enabled )
			{
				while ( amountToCreateAtOnce > 0 && !IsSetup )
				{
					amountToCreateAtOnce = amountToCreateAtOnce - 1;
					if ( SetupFunctions.Count > 0 )
					{
						var callBack = SetupFunctions.Dequeue();
						if ( callBack != null )
						{
							callBack();
						}
					}
					else
					{
						IsSetup = true;
						OnSetup();
					}
				}
			}
		}

	}
}
