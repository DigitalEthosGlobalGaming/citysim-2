using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Sandbox.Code
{
	
	public partial class Player : Component
	{
		public string NickName { get; set; } = "Rotten";
		public float Level { get; set; } = 1;
		public void OnUpdate()
		{
			// Do shit;
		}
	}
}
