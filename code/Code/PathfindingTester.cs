using Degg.GridSystem;
using Degg.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sandbox.Code
{
	[Category("Citysim.Debuggin")]
	public class PathfindingTester: Component, Component.ExecuteInEditor
	{
		[Property]
		public GridMap Map { get; set; }

		[Property]
		public List<Component> Tests { get; set; }
		public float NextRun { get; set; }
		[Property, Title("Run Interval")]
		public float RunInterval { get; set; }

		protected override void OnUpdate()
		{
			base.OnUpdate();
			if ( RunInterval > 0 )
			{
				if ( NextRun < Time.Now )
				{
					NextRun = Time.Now + RunInterval;
					AdvLog.Info( "Running pathfinding test" );
					// Run pathfinding test
					// RunTest();
				}
			}
		}

		protected override void OnDisabled()
		{
			base.OnDisabled();
			var items = Map.GetGridAsList();
			foreach(var item in items)
			{
				item.IsTest = false;
			}
		}

		protected override void OnEnabled()
		{
			base.OnEnabled();
			RunTest();

		}

		public void Cleanup()
		{
			var items = Map.GetGridAsList();
			foreach ( var item in items )
			{
				item.IsTest = false;
			}
		}

		public void RunTest()
		{
			if (!Map?.IsValid() ?? false) { return;}
			var RandomA = Map.GetRandomSpace();
			var RandomB = Map.GetRandomSpace();
			if (RandomA == RandomB) { return; }

			var partItems = Map.CreatePath( RandomA, RandomB );
			AdvLog.Info( $"Pathfinding test from {RandomA.GridPosition} to {RandomB.GridPosition} found {partItems.Count} spaces" );
			var previous = partItems[0];
			foreach (var space in partItems)
			{
				if( previous  == space)
				{

				}
				space.IsTest = true;
				//current = space;
				//space
			}
		}
		
	}
}
