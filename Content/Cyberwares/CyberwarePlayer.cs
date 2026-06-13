using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Cyberwares
{
    /// <summary>玩家义体装备数据 ModPlayer</summary>
    internal class CyberwarePlayer : ModPlayer
    {
        /// <summary>槽位总数，对应 CyberSlotRenderer.Definitions</summary>
        public const int SlotCount = 12;

        /// <summary>最大义体容量</summary>
        public int MaxCapacity = 20;

        /// <summary>各槽位装备，空位为 air</summary>
        public Item[] EquippedCyberwares { get; private set; }

        public override void Initialize() {
            EquippedCyberwares = new Item[SlotCount];
            for (int i = 0; i < SlotCount; i++) {
                EquippedCyberwares[i] = new Item();
            }
        }

        /// <summary>已用容量</summary>
        public int UsedCapacity {
            get {
                int total = 0;
                for (int i = 0; i < SlotCount; i++) {
                    if (EquippedCyberwares[i]?.ModItem is BaseCyberware cyber) {
                        total += cyber.CapacityCost;
                    }
                }
                return total;
            }
        }

        /// <summary>剩余容量</summary>
        public int RemainingCapacity => MaxCapacity - UsedCapacity;

        /// <summary>检查物品能否装入 slot</summary>
        public bool CanEquip(Item item, int slotIndex) {
            if (slotIndex < 0 || slotIndex >= SlotCount) return false;
            if (item?.ModItem is not BaseCyberware cyber) return false;

            //槽位类别匹配
            if ((int)cyber.SlotCategory != slotIndex) return false;

            //容量：先扣旧义体再算新义体
            int currentUsed = UsedCapacity;
            if (EquippedCyberwares[slotIndex]?.ModItem is BaseCyberware oldCyber) {
                currentUsed -= oldCyber.CapacityCost;
            }
            if (currentUsed + cyber.CapacityCost > MaxCapacity) return false;

            return true;
        }

        /// <summary>装入 slot，成功返回 true</summary>
        public bool Equip(Item item, int slotIndex) {
            if (!CanEquip(item, slotIndex)) return false;

            Item cloned = item.Clone();
            EquippedCyberwares[slotIndex] = cloned;

            //克隆实例后 OnEquip
            if (cloned.ModItem is BaseCyberware newCyber) {
                newCyber.OnEquip(Player);
            }

            return true;
        }

        /// <summary>卸载 slot，返回旧物品</summary>
        public Item Unequip(int slotIndex) {
            if (slotIndex < 0 || slotIndex >= SlotCount) return null;

            Item oldItem = EquippedCyberwares[slotIndex];
            if (oldItem == null || oldItem.IsAir) return null;

            //OnUnequip
            if (oldItem.ModItem is BaseCyberware cyber) {
                cyber.OnUnequip(Player);
            }

            EquippedCyberwares[slotIndex] = new Item();
            return oldItem;
        }

        /// <summary>背包中可装入 slot 的义体索引列表</summary>
        public List<int> GetCompatibleItems(int slotIndex) {
            List<int> result = [];
            if (slotIndex < 0 || slotIndex >= SlotCount) return result;

            for (int i = 0; i < Main.InventorySlotsTotal; i++) {
                Item item = Player.inventory[i];
                if (item == null || item.IsAir) continue;
                if (item.ModItem is not BaseCyberware cyber) continue;
                if ((int)cyber.SlotCategory != slotIndex) continue;
                result.Add(i);
            }
            return result;
        }

        public override void PostUpdate() {
            for (int i = 0; i < SlotCount; i++) {
                if (EquippedCyberwares[i]?.ModItem is BaseCyberware cyber) {
                    cyber.UpdateEquipped(Player);
                }
            }
        }

        public override void PostUpdateEquips() {
            //PostUpdateEquips 后立即写入，避免同帧 ResetEffects 抹掉
            for (int i = 0; i < SlotCount; i++) {
                if (EquippedCyberwares[i]?.ModItem is BaseCyberware cyber) {
                    cyber.PostUpdateEquipped(Player);
                }
            }
        }

        public override void SaveData(TagCompound tag) {
            try {
                tag["CyberMaxCapacity"] = MaxCapacity;
                for (int i = 0; i < SlotCount; i++) {
                    Item item = EquippedCyberwares[i];
                    if (item != null && !item.IsAir) {
                        tag[$"Cyber_{i}"] = ItemIO.Save(item);
                    }
                }
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error($"CyberwarePlayer.SaveData Error: {ex.Message}");
            }
        }

        public override void LoadData(TagCompound tag) {
            try {
                if (tag.TryGet("CyberMaxCapacity", out int cap)) {
                    MaxCapacity = Math.Clamp(cap, 0, 1000);
                }
                for (int i = 0; i < SlotCount; i++) {
                    if (tag.TryGet($"Cyber_{i}", out TagCompound itemTag)) {
                        try {
                            EquippedCyberwares[i] = ItemIO.Load(itemTag);
                        } catch {
                            EquippedCyberwares[i] = new Item();
                        }
                    }
                    else {
                        EquippedCyberwares[i] = new Item();
                    }
                }
            } catch (Exception ex) {
                CWRMod.Instance.Logger.Error($"CyberwarePlayer.LoadData Error: {ex.Message}");
            }
        }
    }
}
