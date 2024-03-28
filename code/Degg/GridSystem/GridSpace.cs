
using Degg.Util;

namespace Degg.GridSystem
{

	[Category( "Degg.GridSystem" )]
	public partial class GridSpace: Component
	{
		[Property]
		public Vector2 GridPosition { get; set; }
		[Property]
		public bool Blocked { get; set; }

		public GridMap Map { get; set; }

		public List<GridItem> Items { get; set; } = new List<GridItem>();

		public Vector3 Scale { get; set; }
		public GridMap Parent { get; set; }
		public bool IsTest { get; set; }


		public Vector3 GetWorldPosition()
		{
			return this.Map.GetWorldSpace( (int)this.GridPosition.x, (int)this.GridPosition.y );
		}


		protected override void DrawGizmos()
		{
			base.DrawGizmos();

			GameObject.Transform.Scale = GameObject.Transform.Scale.WithZ( 1 );
			if ( IsTest )
			{
				Gizmo.Draw.Color = Color.Yellow;
				GameObject.Transform.Scale = GameObject.Transform.Scale.WithZ( 5 );
			}
			if ( Blocked )
			{
				Gizmo.Draw.Color = Color.Black;

				GameObject.Transform.Scale = GameObject.Transform.Scale.WithZ( 10 );
			} else
			{

			}

			if ( Blocked | IsTest )
			{
				Gizmo.Draw.SolidSphere( Vector3.Up * 10, 10f );
			}
		}

		public virtual float GetMovementWeight( GridSpace a, NavPoint n )
		{
			if (Blocked)
			{
				return -1;
			}
			if ( a == null )
			{
				return -1;
			}
			return 10;
		}

		public virtual void Tick( float delta )
		{

		}

		public virtual void ClientTick( float delta, float currentTick )
		{

		}
		public virtual void ServerTick( float delta, float currentTick )
		{

		}


		public virtual void OnAddToMap()
		{
			UpdatePosition();
		}

		public void UpdatePosition()
		{
			GameObject.Transform.Position = GetWorldPosition();
		}

		public T GetNeighbour<T>( int x, int y ) where T : GridSpace
		{
			var positionToGet = new Vector2( x, y ) + GridPosition;
			if ( Map == null )
			{
				return null;
			}

			return (T)Map.GetSpace( positionToGet );
		}
		// Grabs immediate neighbours.
		// Note:
		// Up, Right, Down, Left in a clock-wise pattern to grab the neighbours.
		// If a neighbour does not exist, we will place the element as null;
		//	do check if the element in the array is null when using this in a for-loop.
		public T[] GetNeighbours<T>() where T : GridSpace
		{
			var up = GetNeighbour<T>( 0, -1 );
			var down = GetNeighbour<T>( 0, 1 );
			var left = GetNeighbour<T>( -1, 0 );
			var right = GetNeighbour<T>( 1, 0 );
			T[] neighbours = { up, right, down, left };
			return neighbours;
		}
		public GridSpace GetNeighbour( Vector2 pos )
		{
			var positionToGet = pos + GridPosition;
			return Map.GetSpace( positionToGet );
		}

		public void AddItem( GridItem item, bool triggerEvents = true )
		{
			item.Space = this;
			Items.Add( item );
			if ( triggerEvents )
			{
				this.OnItemAdded( item );
				item.OnAdded();
			}
		}

		public void RemoveItem( GridItem item, bool triggerEvents = true )
		{
			item.Space = null;
			Items.Remove( item );
			if ( triggerEvents )
			{
				OnItemRemoved( item );
				item.OnRemove();
			}
		}

		public virtual void OnItemAdded( GridItem item ) { }
		public virtual void OnItemRemoved( GridItem item ) { }

		public void SetParent( GridMap parent ) {
			this.Parent = parent;
			this.GameObject.Parent = parent.GameObject;
		}

		public void Cleanup()
		{
			this.GameObject.Destroy();
		}

		public override string ToString()
		{
			return $"SPACE [{GridPosition.x},{GridPosition.y}]";
		}
	}
}
