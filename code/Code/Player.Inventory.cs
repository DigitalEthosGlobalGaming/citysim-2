using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Sandbox.Code
{
	public class PhysicalItem: GameObject
	{
		public WeaponStats Stats { get; set; }
	}
	public class WeaponStats
	{
		public string Slot { get; set; }
		public float Damage { get; set; }
	}

	public class EquipmentItemSlot
	{

		public string Name { get; set; }
		public WeaponStats? Item { get; set; }

		public bool PickupItem( PhysicalItem item )
		{
			if (Item == null)
			{
				Item = item.Stats;
				item.Destroy();
				return true;
			}
			return false;
		}
		public bool DropItem( WeaponStats item )
		{
			var newObject = new PhysicalItem();
			newObject.Stats = item;
			this.Item = null;
			return true;
		}
	}

	public class PlayerStatusEffect: Component
	{
		public StatusEffects Type { get; set; }
		public float Amount { get; set; }

		public Player ImpactedPlayer { get; set; }
		public float Expiry { get; set; }

		public float NextUpdate { get; set; }
		public virtual float UpdateInterval { get; set; } = 0;

		public bool IsExpired()
		{
			if ( Expiry > Time.Now )
			{
				return true;
			}
			return false;
		}

		public void Update()
		{
			if ( UpdateInterval > 0 )
			{
				if ( NextUpdate < Time.Now )
				{
					NextUpdate = Time.Now + UpdateInterval;
					this.OnUpdate();
				}
			}
			if (IsExpired())
			{
				this.Remove();
			}
		}
		public void Apply()
		{
			this.OnApply();
		}
		public void Remove()
		{
			this.OnRemove();
			ImpactedPlayer.RemoveStatusEffect( this );
		}
		public virtual void OnApply()
		{

		}
		public virtual void OnUpdate()
		{
			
		}
		public virtual void OnRemove()
		{

		}
	}

	public class HardAttackStatusEffect : PlayerStatusEffect
	{
		public string Name { get; set; } = "HardAttack";
		public string Icon { get; set; } = "icon_blind";
		public float Duration { get; set; }
		public override float UpdateInterval { get; set; } = 1.5f;

		public override void OnApply()
		{
			base.OnApply();
			ImpactedPlayer.ShitSelf();
			ImpactedPlayer.Transform.Position = ImpactedPlayer.Transform.Position + Vector3.Up * 10;

			ImpactedPlayer.Bleeding = true;
		}
		public override void OnUpdate()
		{
			base.OnUpdate();
		}


	}
	public enum StatusEffects
	{
		Stun,
		Poison,
		Bleed,
		Slow,
		Root,
		Blind,
	}

	public partial class Player: Component
	{
		public List<EquipmentItemSlot> EquipmentSlots { get; set; } = new List<EquipmentItemSlot>();

		public List<PlayerStatusEffect> StatusEffects = new List<PlayerStatusEffect>();

		public bool Bleeding { get; set; } = false;

		public float BaseDamage { get; set; } = 10;
		public float AddedDamage { get; set; } = 0;
		public void SetupInventory()
		{
			EquipmentSlots = new List<EquipmentItemSlot>
			{
				new EquipmentItemSlot { Name = "Head" },
				new EquipmentItemSlot { Name = "Chest" },
				new EquipmentItemSlot { Name = "Legs" },
				new EquipmentItemSlot { Name = "Feet" },
				new EquipmentItemSlot { Name = "MainHand" },
				new EquipmentItemSlot { Name = "OffHand" },
			};
		}

		public void ShitSelf()
		{

		}

		public void ApplyStatusEffect( PlayerStatusEffect effect )
		{
			StatusEffects.Add( effect );
			effect.ImpactedPlayer = this;
			effect.Apply();
		}
		public void RemoveStatusEffect( PlayerStatusEffect effect )
		{
			StatusEffects.Remove( effect );
			effect.Remove();
		}
		public void UpdateStatusEffect()
		{

		}

		public EquipmentItemSlot? GetEmptyEquipmentItemSlot(PhysicalItem item)
		{
			var slot = GetEquipmentItemSlot( item.Stats.Slot );
			if (slot.Item == null)
			{
				return slot;
			}
			return null;
		}
		public EquipmentItemSlot GetEquipmentItemSlot(string name)
		{
			return EquipmentSlots.Find( x => x.Name == name );
		}

		public void OnPlayerPickup(PhysicalItem item)
		{
			var slot = GetEmptyEquipmentItemSlot( item );
			if (slot != null)
			{
				var didPickup = slot.PickupItem( item );
				if (didPickup)
				{
					RecalculateStats();
				}
			}
		}

		public void RecalculateStats()
		{
			// Loop through the equipment slots, get the item if it exists and then add the damage to the AddedDamage
			AddedDamage = 0;
			foreach (var slot in EquipmentSlots)
			{
				if (slot.Item != null)
				{
					AddedDamage += slot.Item.Damage;
				}
			}
		}
	}
}
