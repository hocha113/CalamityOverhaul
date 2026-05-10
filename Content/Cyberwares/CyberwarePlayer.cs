using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Cyberwares
{
    /// <summary>
    /// 管理玩家义体装备数据的ModPlayer
    /// </summary>
    internal class CyberwarePlayer : ModPlayer
    {
        /// <summary>
        /// 义体槽位总数，对应CyberSlotRenderer.Definitions的12个槽位
        /// </summary>
        public const int SlotCount = 12;

        /// <summary>
        /// 玩家的最大义体容量
        /// </summary>
        public int MaxCapacity = 20;

        /// <summary>
        /// 每个槽位装备的义体物品，空位为air item
        /// </summary>
        public Item[] EquippedCyberwares { get; private set; }

        public override void Initialize() {
            EquippedCyberwares = new Item[SlotCount];
            for (int i = 0; i < SlotCount; i++) {
                EquippedCyberwares[i] = new Item();
            }
        }

        /// <summary>
        /// 计算当前已使用的容量
        /// </summary>
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

        /// <summary>
        /// 剩余可用容量
        /// </summary>
        public int RemainingCapacity => MaxCapacity - UsedCapacity;

        /// <summary>
        /// 检查指定物品是否可以装入指定槽位
        /// </summary>
        public bool CanEquip(Item item, int slotIndex) {
            if (slotIndex < 0 || slotIndex >= SlotCount) return false;
            if (item?.ModItem is not BaseCyberware cyber) return false;

            // 检查槽位类别匹配
            if ((int)cyber.SlotCategory != slotIndex) return false;

            // 检查容量（如果当前槽位已有义体，先扣除旧的容量再计算）
            int currentUsed = UsedCapacity;
            if (EquippedCyberwares[slotIndex]?.ModItem is BaseCyberware oldCyber) {
                currentUsed -= oldCyber.CapacityCost;
            }
            if (currentUsed + cyber.CapacityCost > MaxCapacity) return false;

            return true;
        }

        /// <summary>
        /// 将物品装入指定槽位，返回被替换的旧物品（如有）
        /// </summary>
        public bool Equip(Item item, int slotIndex) {
            if (!CanEquip(item, slotIndex)) return false;

            Item cloned = item.Clone();
            EquippedCyberwares[slotIndex] = cloned;

            // 通知克隆后的实例完成装备
            if (cloned.ModItem is BaseCyberware newCyber) {
                newCyber.OnEquip(Player);
            }

            return true;
        }

        /// <summary>
        /// 卸载指定槽位的义体，返回卸载的物品
        /// </summary>
        public Item Unequip(int slotIndex) {
            if (slotIndex < 0 || slotIndex >= SlotCount) return null;

            Item oldItem = EquippedCyberwares[slotIndex];
            if (oldItem == null || oldItem.IsAir) return null;

            // 通知义体卸载
            if (oldItem.ModItem is BaseCyberware cyber) {
                cyber.OnUnequip(Player);
            }

            EquippedCyberwares[slotIndex] = new Item();
            return oldItem;
        }

        /// <summary>
        /// 获取玩家背包中所有可装入指定槽位的义体物品
        /// </summary>
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
            //义体的统计类加成必须在原版装备结算后立即写入，否则会被同帧的 ResetEffects 抹掉
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
                    MaxCapacity = cap;
                }
                for (int i = 0; i < SlotCount; i++) {
                    if (tag.TryGet($"Cyber_{i}", out TagCompound itemTag)) {
                        EquippedCyberwares[i] = ItemIO.Load(itemTag);
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
