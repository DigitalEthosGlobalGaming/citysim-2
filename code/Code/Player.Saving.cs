using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Sandbox.Code
{
	
	public class PlayerSaveData
	{
		public List<EquipmentItemSlot> EquipmentSlots { get; set; } = new List<EquipmentItemSlot>();
		public string NickName { get; set; }
		public float Level { get; set; }
	}
	public partial class Player : Component
	{
		

		public string GetSaveData()
		{
			var saveData = new PlayerSaveData();
			saveData.EquipmentSlots = EquipmentSlots;
			saveData.NickName = NickName;
			saveData.Level = Level;
			return JsonSerializer.Serialize( saveData );
		}

		public void LoadFromSave(string SaveData)
		{
			LoadFromSave(JsonSerializer.Deserialize<PlayerSaveData>(SaveData));
		}
		public void LoadFromSave(PlayerSaveData data)
		{
			this.Level = data.Level;
			this.EquipmentSlots = data.EquipmentSlots;
			this.Level = data.Level;
		}
	}
}
